using System.Diagnostics;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace MattEland.Jaimes.Workers.DocumentCrackerWorker.Services;

public class DocumentCrackingService(
 ILogger<DocumentCrackingService> logger,
 IMongoClient mongoClient,
 IMessagePublisher messagePublisher,
 ActivitySource activitySource,
 IPdfTextExtractor pdfTextExtractor) : IDocumentCrackingService
{
 // Cache database and collection references to avoid recreating them and prevent connection disposal issues
 private readonly Lazy<IMongoDatabase> _database = new(() => mongoClient.GetDatabase("documents"));
 private readonly Lazy<IMongoCollection<CrackedDocument>> _collection = new(() => 
 mongoClient.GetDatabase("documents").GetCollection<CrackedDocument>("crackedDocuments"));

 private IMongoDatabase Database => _database.Value;
 private IMongoCollection<CrackedDocument> Collection => _collection.Value;

 public async Task ProcessDocumentAsync(string filePath, string? relativeDirectory, string rulesetId, string documentKind, CancellationToken cancellationToken = default)
 {
 logger.LogInformation("Starting to crack document: {FilePath}", filePath);
 
 using Activity? activity = activitySource.StartActivity("DocumentCracking.CrackDocument");
 
 if (activity == null)
 {
 logger.LogWarning("Failed to create activity for document: {FilePath} - ActivitySource may not be registered or sampled", filePath);
 }
 
 FileInfo fileInfo = new(filePath);
 activity?.SetTag("cracker.file_path", filePath);
 activity?.SetTag("cracker.file_name", fileInfo.Name);
 activity?.SetTag("cracker.file_size", fileInfo.Exists ? fileInfo.Length :0);
 activity?.SetTag("cracker.relative_directory", relativeDirectory ?? string.Empty);
 activity?.SetTag("cracker.ruleset_id", rulesetId);
 activity?.SetTag("cracker.document_kind", documentKind);
 
 // Only process PDF files
 if (!Path.GetExtension(filePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
 {
 logger.LogDebug("Skipping unsupported file: {FilePath}", filePath);
 activity?.SetStatus(ActivityStatusCode.Error, "Unsupported file type");
 return;
 }
 
 (string contents, int pageCount) = pdfTextExtractor.ExtractText(filePath);
 
 activity?.SetTag("cracker.page_count", pageCount);

 // Use UpdateOneAsync with upsert to avoid _id conflicts
 // This will update if the document exists (by FilePath) or insert if it doesn't
 FilterDefinition<CrackedDocument> filter = Builders<CrackedDocument>.Filter.Eq(d => d.FilePath, filePath);
 
 // Check existing document to see if content changed and if it's already processed
 CrackedDocument? existingDocument = await Collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
 bool contentChanged = existingDocument == null || existingDocument.Content != contents;
 
 // Update document - reset IsProcessed to false if content changed, otherwise preserve current state
 UpdateDefinition<CrackedDocument> update = Builders<CrackedDocument>.Update
 .Set(d => d.FilePath, filePath)
 .Set(d => d.RelativeDirectory, relativeDirectory ?? string.Empty)
 .Set(d => d.FileName, Path.GetFileName(filePath))
 .Set(d => d.Content, contents)
 .Set(d => d.CrackedAt, DateTime.UtcNow)
 .Set(d => d.FileSize, fileInfo.Length)
 .Set(d => d.PageCount, pageCount)
 .Set(d => d.RulesetId, rulesetId)
 .Set(d => d.DocumentKind, documentKind);
 
 // Reset processed flag only if content changed
 if (contentChanged)
 {
 update = update.Set(d => d.IsProcessed, false);
 }
 
 UpdateOptions updateOptions = new() { IsUpsert = true };
 
 UpdateResult result = await Collection.UpdateOneAsync(filter, update, updateOptions, cancellationToken);
 
 // Get the document ID after upsert
 // If it was an insert, use the UpsertedId; otherwise, query the document by FilePath
 // UpsertedId is a BsonObjectId, so we use ToString() instead of AsString
 string documentId = result.UpsertedId?.ToString() ?? 
 (await Collection.Find(filter).FirstOrDefaultAsync(cancellationToken))?.Id ?? string.Empty;
 
 logger.LogInformation("Cracked and saved to MongoDB: {FilePath} ({PageCount} pages, {FileSize} bytes)", 
 filePath, pageCount, fileInfo.Length);
 
 activity?.SetTag("cracker.document_id", documentId);
 activity?.SetStatus(ActivityStatusCode.Ok);
 
 // Check if document needs processing (not processed yet)
 // Re-query to get the current IsProcessed state after update
 CrackedDocument? updatedDocument = await Collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
 bool needsProcessing = updatedDocument == null || !updatedDocument.IsProcessed;
 
 if (needsProcessing)
 {
 // Publish message to generate documentMetadata
 await PublishDocumentCrackedMessageAsync(documentId, filePath, relativeDirectory ?? string.Empty, 
 Path.GetFileName(filePath), fileInfo.Length, pageCount, documentKind, rulesetId, cancellationToken);
 }
 else
 {
 logger.LogDebug("Document {DocumentId} already processed, skipping enqueue", documentId);
 }
 }
 
 private async Task PublishDocumentCrackedMessageAsync(string documentId, string filePath, 
 string relativeDirectory, string fileName, long fileSize, int pageCount, 
 string documentKind, string rulesetId, CancellationToken cancellationToken)
 {
 try
 {
 // Create message
 DocumentReadyForChunkingMessage message = new()
 {
 DocumentId = documentId,
 FilePath = filePath,
 FileName = fileName,
 RelativeDirectory = relativeDirectory,
 FileSize = fileSize,
 PageCount = pageCount,
 CrackedAt = DateTime.UtcNow,
 DocumentKind = documentKind,
 RulesetId = rulesetId
 };
 
 // Publish using message publisher
 await messagePublisher.PublishAsync(message, cancellationToken);
 
 logger.LogInformation("Successfully published document ready for chunking message. DocumentId: {DocumentId}, FilePath: {FilePath}", 
 documentId, filePath);
 }
 catch (Exception ex)
 {
 // Log error but don't fail the document cracking process
 logger.LogError(ex, "Failed to publish document ready for chunking message: {FilePath}", filePath);
 }
 }
}

