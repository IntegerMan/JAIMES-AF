using System.Diagnostics;
using System.Text;
using MassTransit;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace MattEland.Jaimes.Workers.DocumentCrackerWorker.Services;

public class DocumentCrackingService(
    ILogger<DocumentCrackingService> logger,
    IMongoClient mongoClient,
    IPublishEndpoint publishEndpoint,
    ActivitySource activitySource) : IDocumentCrackingService
{
    public async Task ProcessDocumentAsync(string filePath, string? relativeDirectory, CancellationToken cancellationToken = default)
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
        
        // Only process PDF files
        if (!Path.GetExtension(filePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogDebug("Skipping unsupported file: {FilePath}", filePath);
            activity?.SetStatus(ActivityStatusCode.Error, "Unsupported file type");
            return;
        }
        
        (string contents, int pageCount) = ExtractPdfText(filePath);
        
        activity?.SetTag("cracker.page_count", pageCount);

        // Get database from connection string - the database name is "documents" as configured in AppHost
        IMongoDatabase mongoDatabase = mongoClient.GetDatabase("documents");
        IMongoCollection<CrackedDocument> collection = mongoDatabase.GetCollection<CrackedDocument>("crackedDocuments");

        // Use UpdateOneAsync with upsert to avoid _id conflicts
        // This will update if the document exists (by FilePath) or insert if it doesn't
        FilterDefinition<CrackedDocument> filter = Builders<CrackedDocument>.Filter.Eq(d => d.FilePath, filePath);
        UpdateDefinition<CrackedDocument> update = Builders<CrackedDocument>.Update
            .Set(d => d.FilePath, filePath)
            .Set(d => d.RelativeDirectory, relativeDirectory ?? string.Empty)
            .Set(d => d.FileName, Path.GetFileName(filePath))
            .Set(d => d.Content, contents)
            .Set(d => d.CrackedAt, DateTime.UtcNow)
            .Set(d => d.FileSize, fileInfo.Length)
            .Set(d => d.PageCount, pageCount);
        
        UpdateOptions updateOptions = new() { IsUpsert = true };
        
        UpdateResult result = await collection.UpdateOneAsync(filter, update, updateOptions, cancellationToken);
        
        // Get the document ID after upsert
        // If it was an insert, use the UpsertedId; otherwise, query the document by FilePath
        string documentId = result.UpsertedId?.AsString ?? 
            (await collection.Find(filter).FirstOrDefaultAsync(cancellationToken))?.Id ?? string.Empty;
        
        logger.LogInformation("Cracked and saved to MongoDB: {FilePath} ({PageCount} pages, {FileSize} bytes)", 
            filePath, pageCount, fileInfo.Length);
        
        activity?.SetTag("cracker.document_id", documentId);
        activity?.SetStatus(ActivityStatusCode.Ok);
        
        // Publish message using MassTransit
        await PublishDocumentCrackedMessageAsync(documentId, filePath, relativeDirectory ?? string.Empty, 
            Path.GetFileName(filePath), fileInfo.Length, pageCount, cancellationToken);
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
}

