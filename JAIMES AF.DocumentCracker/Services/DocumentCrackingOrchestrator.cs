using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using MassTransit;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.DocumentProcessing.Options;
using MattEland.Jaimes.DocumentProcessing.Services;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace MattEland.Jaimes.DocumentCracker.Services;

public class DocumentCrackingOrchestrator(
    ILogger<DocumentCrackingOrchestrator> logger,
    IDirectoryScanner directoryScanner,
    DocumentScanOptions options,
    IMongoClient mongoClient,
    IPublishEndpoint publishEndpoint,
    ActivitySource activitySource)
{
    public async Task<DocumentCrackingSummary> CrackAllAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.SourceDirectory))
        {
            throw new InvalidOperationException("SourceDirectory configuration is required for document cracking.");
        }

        // Get database from connection string - the database name is "documents" as configured in AppHost
        IMongoDatabase mongoDatabase = mongoClient.GetDatabase("documents");
        IMongoCollection<CrackedDocument> collection = mongoDatabase.GetCollection<CrackedDocument>("crackedDocuments");

        DocumentCrackingSummary summary = new();

        List<string> directories = directoryScanner
            .GetSubdirectories(options.SourceDirectory)
            .ToList();
        directories.Insert(0, options.SourceDirectory);

        foreach (string directory in directories)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            string relativeDirectory = Path.GetRelativePath(options.SourceDirectory, directory);
            if (relativeDirectory == ".")
            {
                relativeDirectory = string.Empty;
            }

            using Activity? directoryActivity = activitySource.StartActivity("DocumentCracking.ProcessDirectory");
            directoryActivity?.SetTag("cracker.directory", directory);
            directoryActivity?.SetTag("cracker.relative_directory", relativeDirectory);

            IEnumerable<string> files = directoryScanner.GetFiles(directory, options.SupportedExtensions);
            int directoryCracked = 0;
            int directoryFailures = 0;
            
            foreach (string filePath in files)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                summary.TotalDiscovered++;

                if (!Path.GetExtension(filePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogDebug("Skipping unsupported file: {FilePath}", filePath);
                    summary.SkippedUnsupported++;
                    continue;
                }

                try
                {
                    await CrackDocumentAsync(filePath, relativeDirectory, collection, cancellationToken);
                    summary.TotalCracked++;
                    directoryCracked++;
                }
                catch (Exception ex)
                {
                    summary.TotalFailures++;
                    directoryFailures++;
                    logger.LogError(ex, "Failed to crack document: {FilePath}", filePath);
                }
            }
            
            if (directoryActivity != null)
            {
                directoryActivity.SetTag("cracker.directory_cracked", directoryCracked);
                directoryActivity.SetTag("cracker.directory_failures", directoryFailures);
                directoryActivity.SetStatus(directoryFailures > 0 ? ActivityStatusCode.Error : ActivityStatusCode.Ok);
            }
        }

        return summary;
    }

    private async Task CrackDocumentAsync(string filePath, string relativeDirectory, IMongoCollection<CrackedDocument> collection, CancellationToken cancellationToken)
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
        activity?.SetTag("cracker.relative_directory", relativeDirectory);
        
        (string contents, int pageCount) = ExtractPdfText(filePath);
        
        activity?.SetTag("cracker.page_count", pageCount);

        // Use UpdateOneAsync with upsert to avoid _id conflicts
        // This will update if the document exists (by FilePath) or insert if it doesn't
        FilterDefinition<CrackedDocument> filter = Builders<CrackedDocument>.Filter.Eq(d => d.FilePath, filePath);
        
        // Check existing document to see if content changed and if it's already processed
        CrackedDocument? existingDocument = await collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
        bool contentChanged = existingDocument == null || existingDocument.Content != contents;
        
        // Update document - reset IsProcessed to false if content changed, otherwise preserve current state
        UpdateDefinition<CrackedDocument> update = Builders<CrackedDocument>.Update
            .Set(d => d.FilePath, filePath)
            .Set(d => d.RelativeDirectory, relativeDirectory)
            .Set(d => d.FileName, Path.GetFileName(filePath))
            .Set(d => d.Content, contents)
            .Set(d => d.CrackedAt, DateTime.UtcNow)
            .Set(d => d.FileSize, fileInfo.Length)
            .Set(d => d.PageCount, pageCount);
        
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
            // Publish message using MassTransit to generate documentMetadata
            await PublishDocumentCrackedMessageAsync(documentId, filePath, relativeDirectory, 
                Path.GetFileName(filePath), fileInfo.Length, pageCount, cancellationToken);
        }
        else
        {
            logger.LogDebug("Document {DocumentId} already processed, skipping enqueue", documentId);
        }
    }
    
    private async Task PublishDocumentCrackedMessageAsync(string documentId, string filePath, 
        string relativeDirectory, string fileName, long fileSize, int pageCount, 
        CancellationToken cancellationToken)
    {
        try
        {
            // Create message
            DocumentCrackedMessage message = new()
            {
                DocumentId = documentId,
                FilePath = filePath,
                FileName = fileName,
                RelativeDirectory = relativeDirectory,
                FileSize = fileSize,
                PageCount = pageCount,
                CrackedAt = DateTime.UtcNow
            };
            
            // Publish using MassTransit - it will handle serialization and routing
            await publishEndpoint.Publish(message, cancellationToken);
            
            logger.LogInformation("Successfully published document cracked message via MassTransit. DocumentId: {DocumentId}, FilePath: {FilePath}", 
                documentId, filePath);
        }
        catch (Exception ex)
        {
            // Log error but don't fail the document cracking process
            logger.LogError(ex, "Failed to publish document cracked message via MassTransit: {FilePath}", filePath);
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

    public class DocumentCrackingSummary
    {
        public int TotalDiscovered { get; set; }
        public int TotalCracked { get; set; }
        public int TotalFailures { get; set; }
        public int SkippedUnsupported { get; set; }
    }
}


