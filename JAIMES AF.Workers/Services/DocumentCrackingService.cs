using System.Diagnostics;
using MattEland.Jaimes.ServiceDefinitions.Configuration;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MattEland.Jaimes.Workers.Services;

public class DocumentCrackingService(
    ILogger<DocumentCrackingService> logger,
    IDbContextFactory<JaimesDbContext> dbContextFactory,
    IMessagePublisher messagePublisher,
    ActivitySource activitySource,
    IPdfTextExtractor pdfTextExtractor,
    IOptions<DocumentCrackingOptions> options) : IDocumentCrackingService
{
    private readonly DocumentCrackingOptions _options = options.Value;

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

        // Sanitize content to remove null bytes and other problematic characters for PostgreSQL UTF-8 encoding
        contents = SanitizeContentForPostgreSQL(contents);

        activity?.SetTag("cracker.page_count", pageCount);

        await using JaimesDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Check if document already exists
        CrackedDocument? existingDocument = await dbContext.CrackedDocuments
            .FirstOrDefaultAsync(d => d.FilePath == filePath, cancellationToken);

        bool contentChanged = existingDocument == null || existingDocument.Content != contents;

        CrackedDocument documentEntity;

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

            documentEntity = existingDocument;
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
            documentEntity = newDocument;
        }

        // Upload document file to database if enabled and document is missing stored file
        // This handles both new documents and existing documents that need their file uploaded
        if (_options.UploadDocumentsWhenCracking && documentEntity.StoredFileId == null)
        {
            await UploadDocumentFileAsync(dbContext, documentEntity, filePath, fileInfo, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // Get the document ID after save
        int documentId = documentEntity.Id;

        if (documentId == 0)
        {
            // For new documents, query the database to get the saved document with its ID
            CrackedDocument? savedDocument = await dbContext.CrackedDocuments
                .FirstOrDefaultAsync(d => d.FilePath == filePath, cancellationToken);

            if (savedDocument == null)
            {
                logger.LogError("Failed to retrieve document after SaveChangesAsync. FilePath: {FilePath}", filePath);
                throw new InvalidOperationException($"Document was saved but could not be retrieved from database. FilePath: {filePath}");
            }

            if (savedDocument.Id == 0)
            {
                logger.LogError("Document ID is 0 after save. FilePath: {FilePath}", filePath);
                throw new InvalidOperationException($"Document ID is invalid (0) after save. FilePath: {filePath}");
            }

            documentId = savedDocument.Id;
            documentEntity = savedDocument;
        }

        logger.LogInformation("Cracked and saved to PostgreSQL: {FilePath} (DocumentId: {DocumentId}, {PageCount} pages, {FileSize} bytes)",
            filePath, documentId, pageCount, fileInfo.Length);

        activity?.SetTag("cracker.document_id", documentId);
        activity?.SetStatus(ActivityStatusCode.Ok);

        // Check if document needs processing (not processed yet)
        bool needsProcessing = !documentEntity.IsProcessed;

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

    private async Task UploadDocumentFileAsync(
        JaimesDbContext dbContext,
        CrackedDocument documentEntity,
        string filePath,
        FileInfo fileInfo,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogDebug("Uploading document file to database: {FilePath}", filePath);

            // Read the file bytes
            byte[] fileBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);

            // Create the stored file
            StoredFile storedFile = new()
            {
                ItemKind = "SourcebookDocument",
                FileName = fileInfo.Name,
                ContentType = "application/pdf",
                BinaryContent = fileBytes,
                CreatedAt = DateTime.UtcNow,
                SizeBytes = fileInfo.Length
            };

            dbContext.StoredFiles.Add(storedFile);

            // Link to the cracked document
            documentEntity.StoredFile = storedFile;

            logger.LogInformation("Document file queued for upload: {FilePath} ({Size} bytes)",
                filePath, fileInfo.Length);
        }
        catch (Exception ex)
        {
            // Log error but don't fail the cracking process
            logger.LogWarning(ex, "Failed to upload document file to database: {FilePath}. Document cracking will continue.", filePath);
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

    /// <summary>
    /// Sanitizes content by removing null bytes and other characters that are invalid for PostgreSQL UTF-8 encoding.
    /// PostgreSQL does not allow null bytes (0x00) in UTF-8 text fields.
    /// </summary>
    private static string SanitizeContentForPostgreSQL(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return content;
        }

        // Remove null bytes (0x00) which PostgreSQL rejects in UTF-8 text
        // Also remove other control characters that might cause issues
        return content
            .Replace("\0", string.Empty) // Remove null bytes
            .Replace("\u0000", string.Empty); // Remove null characters (alternative representation)
    }
}
