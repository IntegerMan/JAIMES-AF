using System.Diagnostics;
using System.Text;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace MattEland.Jaimes.Workers.DocumentCrackerWorker.Services;

public class DocumentCrackingService(
    ILogger<DocumentCrackingService> logger,
    IMongoClient mongoClient,
    IMessagePublisher messagePublisher,
    ActivitySource activitySource) : IDocumentCrackingService
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
        activity?.SetTag("cracker.file_size", fileInfo.Exists ? fileInfo.Length : 0);
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
        
        (string contents, int pageCount) = ExtractPdfText(filePath);
        
        activity?.SetTag("cracker.page_count", pageCount);

        // Use UpdateOneAsync with upsert to avoid _id conflicts
        // This will update if the document exists (by FilePath) or insert if it doesn't
        FilterDefinition<CrackedDocument> filter = Builders<CrackedDocument>.Filter.Eq(d => d.FilePath, filePath);
        
        // Check existing document to see if content changed and if it's already processed
        // Add retry logic for MongoDB connection issues
        CrackedDocument? existingDocument = await RetryMongoOperationAsync(
            () => Collection.Find(filter).FirstOrDefaultAsync(cancellationToken),
            "finding existing document",
            cancellationToken);
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
        
        UpdateResult result = await RetryMongoOperationAsync(
            () => Collection.UpdateOneAsync(filter, update, updateOptions, cancellationToken),
            "updating document",
            cancellationToken);
        
        // Get the document ID after upsert
        // If it was an insert, use the UpsertedId; otherwise, query the document by FilePath
        // UpsertedId is a BsonObjectId, so we use ToString() instead of AsString
        string documentId = result.UpsertedId?.ToString() ?? 
            (await RetryMongoOperationAsync(
                () => Collection.Find(filter).FirstOrDefaultAsync(cancellationToken),
                "finding document after update",
                cancellationToken))?.Id ?? string.Empty;
        
        logger.LogInformation("Cracked and saved to MongoDB: {FilePath} ({PageCount} pages, {FileSize} bytes)", 
            filePath, pageCount, fileInfo.Length);
        
        activity?.SetTag("cracker.document_id", documentId);
        activity?.SetStatus(ActivityStatusCode.Ok);
        
        // Check if document needs processing (not processed yet)
        // Re-query to get the current IsProcessed state after update
        CrackedDocument? updatedDocument = await RetryMongoOperationAsync(
            () => Collection.Find(filter).FirstOrDefaultAsync(cancellationToken),
            "checking if document needs processing",
            cancellationToken);
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

    /// <summary>
    /// Retries MongoDB operations that may fail due to connection disposal issues.
    /// This handles intermittent ObjectDisposedException errors.
    /// </summary>
    private async Task<T> RetryMongoOperationAsync<T>(
        Func<Task<T>> operation,
        string operationDescription,
        CancellationToken cancellationToken,
        int maxRetries = 3,
        int delayMs = 100)
    {
        int attempt = 0;
        while (true)
        {
            try
            {
                return await operation();
            }
            catch (ObjectDisposedException ex) when (attempt < maxRetries)
            {
                attempt++;
                logger.LogWarning(ex, 
                    "MongoDB connection disposed during {OperationDescription}. Retrying (attempt {Attempt}/{MaxRetries})...", 
                    operationDescription, attempt, maxRetries);
                
                // Exponential backoff
                int delay = delayMs * (int)Math.Pow(2, attempt - 1);
                await Task.Delay(delay, cancellationToken);
            }
            catch (MongoException ex) when (attempt < maxRetries && 
                (ex.Message.Contains("disposed", StringComparison.OrdinalIgnoreCase) ||
                 ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase)))
            {
                attempt++;
                logger.LogWarning(ex, 
                    "MongoDB connection error during {OperationDescription}. Retrying (attempt {Attempt}/{MaxRetries})...", 
                    operationDescription, attempt, maxRetries);
                
                // Exponential backoff
                int delay = delayMs * (int)Math.Pow(2, attempt - 1);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

}

