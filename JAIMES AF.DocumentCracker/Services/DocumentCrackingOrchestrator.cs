using System.Diagnostics;
using System.Linq;
using System.Text;
using MattEland.Jaimes.DocumentProcessing.Options;
using MattEland.Jaimes.DocumentProcessing.Services;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace MattEland.Jaimes.DocumentCracker.Services;

public class DocumentCrackingOrchestrator(
    ILogger<DocumentCrackingOrchestrator> logger,
    IDirectoryScanner directoryScanner,
    DocumentScanOptions options,
    JaimesDbContext dbContext,
    IMessagePublisher messagePublisher,
    ActivitySource activitySource)
{
    public async Task<DocumentCrackingSummary> CrackAllAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.SourceDirectory))
        {
            throw new InvalidOperationException("SourceDirectory configuration is required for document cracking.");
        }

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
                    string rulesetId = DocumentMetadataExtractor.ExtractRulesetId(relativeDirectory);
                    string documentKind = DocumentMetadataExtractor.DetermineDocumentKind(relativeDirectory);
                    await CrackDocumentAsync(filePath, relativeDirectory, rulesetId, documentKind, cancellationToken);
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

    private async Task CrackDocumentAsync(string filePath, string relativeDirectory, string rulesetId, string documentKind, CancellationToken cancellationToken)
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
        activity?.SetTag("cracker.ruleset_id", rulesetId);
        activity?.SetTag("cracker.document_kind", documentKind);

        // Check if document already exists
        CrackedDocument? existingDocument = await dbContext.CrackedDocuments
            .FirstOrDefaultAsync(d => d.FilePath == filePath, cancellationToken);
        
        bool contentChanged = existingDocument == null || existingDocument.Content != contents;
        
        if (existingDocument != null)
        {
            // Update existing document
            existingDocument.RelativeDirectory = relativeDirectory;
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
                RelativeDirectory = relativeDirectory,
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
            await PublishDocumentCrackedMessageAsync(documentId, filePath, relativeDirectory, 
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


