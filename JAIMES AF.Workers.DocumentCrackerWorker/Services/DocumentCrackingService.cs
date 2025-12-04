using System.Diagnostics;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using Microsoft.EntityFrameworkCore;

namespace MattEland.Jaimes.Workers.DocumentCrackerWorker.Services;

public class DocumentCrackingService(
    ILogger<DocumentCrackingService> logger,
    JaimesDbContext dbContext,
    IMessagePublisher messagePublisher,
    ActivitySource activitySource,
    IPdfTextExtractor pdfTextExtractor) : IDocumentCrackingService
{
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
        
        (string contents, int pageCount) = pdfTextExtractor.ExtractText(filePath);
        
        activity?.SetTag("cracker.page_count", pageCount);

        // Check if document already exists
        CrackedDocument? existingDocument = await dbContext.CrackedDocuments
            .FirstOrDefaultAsync(d => d.FilePath == filePath, cancellationToken);
        
        bool contentChanged = existingDocument == null || existingDocument.Content != contents;
        
        if (existingDocument != null)
        {
            // Update existing document
            existingDocument.RelativeDirectory = relativeDirectory ?? string.Empty;
            existingDocument.FileName = Path.GetFileName(filePath);
            existingDocument.Content = contents;
            existingDocument.CrackedAt = DateTime.UtcNow;
            existingDocument.FileSize = fileInfo.Length;
            existingDocument.PageCount = pageCount;
            existingDocument.RulesetId = rulesetId;
            existingDocument.DocumentKind = documentKind;
            
            // Reset processed flag only if content changed
            if (contentChanged)
            {
                existingDocument.IsProcessed = false;
            }
        }
        else
        {
            // Create new document
            CrackedDocument newDocument = new()
            {
                FilePath = filePath,
                RelativeDirectory = relativeDirectory ?? string.Empty,
                FileName = Path.GetFileName(filePath),
                Content = contents,
                CrackedAt = DateTime.UtcNow,
                FileSize = fileInfo.Length,
                PageCount = pageCount,
                RulesetId = rulesetId,
                DocumentKind = documentKind,
                IsProcessed = false
            };
            dbContext.CrackedDocuments.Add(newDocument);
        }
        
        await dbContext.SaveChangesAsync(cancellationToken);
        
        // Get the document ID after save
        CrackedDocument? savedDocument = await dbContext.CrackedDocuments
            .FirstOrDefaultAsync(d => d.FilePath == filePath, cancellationToken);
        
        int documentId = savedDocument?.Id ?? 0;
        
        logger.LogInformation("Cracked and saved to PostgreSQL: {FilePath} ({PageCount} pages, {FileSize} bytes)", 
            filePath, pageCount, fileInfo.Length);
        
        activity?.SetTag("cracker.document_id", documentId);
        activity?.SetStatus(ActivityStatusCode.Ok);
        
        // Check if document needs processing (not processed yet)
        bool needsProcessing = savedDocument == null || !savedDocument.IsProcessed;
        
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
    
    private async Task PublishDocumentCrackedMessageAsync(int documentId, string filePath, 
        string relativeDirectory, string fileName, long fileSize, int pageCount, 
        string documentKind, string rulesetId, CancellationToken cancellationToken)
    {
        try
        {
            // Create message
            DocumentReadyForChunkingMessage message = new()
            {
                DocumentId = documentId.ToString(),
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
