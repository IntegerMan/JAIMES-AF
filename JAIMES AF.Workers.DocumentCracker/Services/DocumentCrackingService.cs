using System.Diagnostics;
using System.Text;
using MattEland.Jaimes.DocumentProcessing.Services;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace MattEland.Jaimes.Workers.DocumentCracker.Services;

public class DocumentCrackingService(
    ILogger<DocumentCrackingService> logger,
    IMongoClient mongoClient,
    IMessagePublisher messagePublisher,
    ActivitySource activitySource) : IDocumentCrackingService
{
    public async Task ProcessDocumentAsync(string filePath, string? relativeDirectory, string? documentType = null, string? rulesetId = null, CancellationToken cancellationToken = default)
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
        activity?.SetTag("cracker.file_size", fileInfo.Exists ? fileInfo.Length : 0);
        activity?.SetTag("cracker.relative_directory", relativeDirectory ?? string.Empty);
        activity?.SetTag("cracker.document_type", documentType ?? string.Empty);
        activity?.SetTag("cracker.ruleset_id", rulesetId ?? string.Empty);
        
        // Only process PDF files
        if (!Path.GetExtension(filePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogDebug("Skipping unsupported file: {FilePath}", filePath);
            activity?.SetStatus(ActivityStatusCode.Error, "Unsupported file type");
            return;
        }
        
        (string contents, int pageCount) = ExtractPdfText(filePath);
        
        activity?.SetTag("cracker.page_count", pageCount);

        // Extract metadata from relative directory
        string rulesetId = DocumentMetadataExtractor.ExtractRulesetId(relativeDirectory);
        string documentKind = DocumentMetadataExtractor.DetermineDocumentKind(relativeDirectory);
        
        activity?.SetTag("cracker.ruleset_id", rulesetId);
        activity?.SetTag("cracker.document_kind", documentKind);

        // Get database from connection string - the database name is "documents" as configured in AppHost
        IMongoDatabase mongoDatabase = mongoClient.GetDatabase("documents");
        IMongoCollection<CrackedDocument> collection = mongoDatabase.GetCollection<CrackedDocument>("crackedDocuments");

        // Use UpdateOneAsync with upsert to avoid _id conflicts
        // This will update if the document exists (by FilePath) or insert if it doesn't
        FilterDefinition<CrackedDocument> filter = Builders<CrackedDocument>.Filter.Eq(d => d.FilePath, filePath);
        
        // Check existing document to see if content changed and if it's already processed
        CrackedDocument? existingDocument = await collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
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
        
        UpdateResult result = await collection.UpdateOneAsync(filter, update, updateOptions, cancellationToken);
        
        // Get the document ID after upsert
        // If it was an insert, use the UpsertedId; otherwise, query the document by FilePath
        // UpsertedId is a BsonObjectId, so we use ToString() instead of AsString
        string documentId = result.UpsertedId?.ToString() ?? 
            (await collection.Find(filter).FirstOrDefaultAsync(cancellationToken))?.Id ?? string.Empty;
        
        logger.LogInformation("Cracked and saved to MongoDB: {FilePath} ({PageCount} pages, {FileSize} bytes)", 
            filePath, pageCount, fileInfo.Length);
        
        activity?.SetTag("cracker.document_id", documentId);
        activity?.SetStatus(ActivityStatusCode.Ok);
        
        // Check if document needs processing (not processed yet)
        // Re-query to get the current IsProcessed state after update
        CrackedDocument? updatedDocument = await collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
        bool needsProcessing = updatedDocument == null || !updatedDocument.IsProcessed;
        
        if (needsProcessing)
        {
            // Publish message to generate documentMetadata
            // Use documentKind as documentType for the message (message still uses DocumentType field)
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
        string? documentType, string? rulesetId, CancellationToken cancellationToken)
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
                DocumentType = documentType,
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

    private static (string content, int pageCount) ExtractPdfText(string filePath)
    {
        StringBuilder builder = new();
        using PdfDocument document = PdfDocument.Open(filePath);
        int pageCount = 0;
        
        foreach (Page page in document.GetPages())
        {
            pageCount++;
            builder.AppendLine($"--- Page {page.Number} ---");
            string pageText = ContentOrderTextExtractor.GetText(page);
            builder.AppendLine(pageText);
            builder.AppendLine();
        }

        return (builder.ToString(), pageCount);
    }

}




