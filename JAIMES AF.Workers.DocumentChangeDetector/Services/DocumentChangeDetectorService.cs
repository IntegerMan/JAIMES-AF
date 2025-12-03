using System.Diagnostics;
using MattEland.Jaimes.DocumentProcessing.Services;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Models;
using MattEland.Jaimes.Workers.DocumentChangeDetector.Configuration;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace MattEland.Jaimes.Workers.DocumentChangeDetector.Services;

public class DocumentChangeDetectorService(
    ILogger<DocumentChangeDetectorService> logger,
    IDirectoryScanner directoryScanner,
    IChangeTracker changeTracker,
    JaimesDbContext dbContext,
    IMessagePublisher messagePublisher,
    ActivitySource activitySource,
    DocumentChangeDetectorOptions options) : IDocumentChangeDetectorService
{
    public async Task<DocumentScanSummary> ScanAndEnqueueAsync(string contentDirectory, CancellationToken cancellationToken = default)
    {
        DocumentScanSummary summary = new();
        
        if (string.IsNullOrWhiteSpace(contentDirectory))
        {
            throw new InvalidOperationException("ContentDirectory is required for document scanning.");
        }

        if (!Directory.Exists(contentDirectory))
        {
            logger.LogError("Content directory does not exist: {ContentDirectory}", contentDirectory);
            throw new DirectoryNotFoundException($"Content directory does not exist: {contentDirectory}");
        }

        using Activity? scanActivity = activitySource.StartActivity("DocumentChangeDetector.ScanDirectory");
        scanActivity?.SetTag("scanner.content_directory", contentDirectory);
        scanActivity?.SetTag("scanner.supported_extensions", string.Join(", ", options.SupportedExtensions));

        // Get all directories to scan (including root)
        List<string> directories = directoryScanner.GetSubdirectories(contentDirectory).ToList();
        directories.Insert(0, contentDirectory);

        foreach (string directory in directories)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("Scan cancelled");
                break;
            }

            await ProcessDirectoryAsync(directory, contentDirectory, summary, cancellationToken);
        }

        if (scanActivity != null)
        {
            scanActivity.SetTag("scanner.files_scanned", summary.FilesScanned);
            scanActivity.SetTag("scanner.files_enqueued", summary.FilesEnqueued);
            scanActivity.SetTag("scanner.files_unchanged", summary.FilesUnchanged);
            scanActivity.SetTag("scanner.errors", summary.Errors);
            scanActivity.SetStatus(summary.Errors > 0 ? ActivityStatusCode.Error : ActivityStatusCode.Ok);
        }

        logger.LogInformation(
            "Document scan completed. Scanned: {FilesScanned}, Enqueued: {FilesEnqueued}, Unchanged: {FilesUnchanged}, Errors: {Errors}",
            summary.FilesScanned, summary.FilesEnqueued, summary.FilesUnchanged, summary.Errors);

        return summary;
    }

    private async Task ProcessDirectoryAsync(
        string directory,
        string rootDirectory,
        DocumentScanSummary summary,
        CancellationToken cancellationToken)
    {
        IEnumerable<string> files = directoryScanner.GetFiles(directory, options.SupportedExtensions);

        foreach (string filePath in files)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            summary.FilesScanned++;

            using Activity? fileActivity = activitySource.StartActivity("DocumentChangeDetector.ProcessFile");
            fileActivity?.SetTag("scanner.file_path", filePath);

            try
            {
                // Compute hash
                string currentHash = await changeTracker.ComputeFileHashAsync(filePath, cancellationToken);
                fileActivity?.SetTag("scanner.file_hash", currentHash);

                // Get stored metadata
                DocumentMetadata? storedMetadata = await GetStoredMetadataAsync(filePath, cancellationToken);

                // Check if file has changed
                if (storedMetadata != null && storedMetadata.Hash == currentHash)
                {
                    // File hash matches - check if document was successfully cracked
                    bool isCracked = await IsDocumentCrackedAsync(filePath, cancellationToken);
                    
                    if (isCracked)
                    {
                        // File unchanged and successfully cracked, update last scanned time
                        string? relativeDirectory = GetRelativeDirectory(filePath, rootDirectory);
                        await UpdateMetadataAsync(filePath, currentHash, relativeDirectory, cancellationToken);
                        summary.FilesUnchanged++;
                        logger.LogDebug("File unchanged and cracked, skipping: {FilePath}", filePath);
                        fileActivity?.SetTag("scanner.status", "unchanged");
                    }
                    else
                    {
                        // File hash matches but document wasn't cracked (likely failed previously)
                        // Enqueue for retry
                        string? relativeDirectory = GetRelativeDirectory(filePath, rootDirectory);
                        await EnqueueDocumentAsync(filePath, relativeDirectory, cancellationToken);
                        await UpdateMetadataAsync(filePath, currentHash, relativeDirectory, cancellationToken);
                        summary.FilesEnqueued++;
                        logger.LogInformation("File hash unchanged but not cracked, enqueuing for retry: {FilePath} (Hash: {Hash})", filePath, currentHash);
                        fileActivity?.SetTag("scanner.status", "retry_uncracked");
                    }
                }
                else
                {
                    // File is new or changed, enqueue for processing
                    string? relativeDirectory = GetRelativeDirectory(filePath, rootDirectory);
                    await EnqueueDocumentAsync(filePath, relativeDirectory, cancellationToken);
                    await UpdateMetadataAsync(filePath, currentHash, relativeDirectory, cancellationToken);
                    summary.FilesEnqueued++;
                    logger.LogInformation("File enqueued for processing: {FilePath} (Hash: {Hash})", filePath, currentHash);
                    fileActivity?.SetTag("scanner.status", storedMetadata == null ? "new" : "changed");
                }

                fileActivity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (Exception ex)
            {
                summary.Errors++;
                logger.LogError(ex, "Error processing file: {FilePath}", filePath);
                fileActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            }
        }
    }

    private async Task<DocumentMetadata?> GetStoredMetadataAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        return await dbContext.DocumentMetadata
            .FirstOrDefaultAsync(x => x.FilePath == filePath, cancellationToken);
    }

    private async Task<bool> IsDocumentCrackedAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        CrackedDocument? crackedDocument = await dbContext.CrackedDocuments
            .FirstOrDefaultAsync(x => x.FilePath == filePath, cancellationToken);
        return crackedDocument != null && !string.IsNullOrWhiteSpace(crackedDocument.Content);
    }

    private async Task UpdateMetadataAsync(
        string filePath,
        string hash,
        string? relativeDirectory,
        CancellationToken cancellationToken)
    {
        string rulesetId = DocumentMetadataExtractor.ExtractRulesetId(relativeDirectory);
        string documentKind = DocumentMetadataExtractor.DetermineDocumentKind(relativeDirectory);

        DocumentMetadata? existingMetadata = await dbContext.DocumentMetadata
            .FirstOrDefaultAsync(x => x.FilePath == filePath, cancellationToken);

        if (existingMetadata != null)
        {
            existingMetadata.Hash = hash;
            existingMetadata.LastScanned = DateTime.UtcNow;
            existingMetadata.RulesetId = rulesetId;
            existingMetadata.DocumentKind = documentKind;
        }
        else
        {
            DocumentMetadata newMetadata = new()
            {
                FilePath = filePath,
                Hash = hash,
                LastScanned = DateTime.UtcNow,
                RulesetId = rulesetId,
                DocumentKind = documentKind
            };
            dbContext.DocumentMetadata.Add(newMetadata);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnqueueDocumentAsync(
        string filePath,
        string? relativeDirectory,
        CancellationToken cancellationToken)
    {
        string extractedRulesetId = DocumentMetadataExtractor.ExtractRulesetId(relativeDirectory);
        string extractedDocumentKind = DocumentMetadataExtractor.DetermineDocumentKind(relativeDirectory);

        CrackDocumentMessage message = new()
        {
            FilePath = filePath,
            RelativeDirectory = relativeDirectory,
            RulesetId = extractedRulesetId,
            DocumentKind = extractedDocumentKind
        };

        await messagePublisher.PublishAsync(message, cancellationToken);
        logger.LogDebug("Published CrackDocumentMessage for: {FilePath}", filePath);
    }

    private static string? GetRelativeDirectory(string filePath, string rootDirectory)
    {
        if (!Path.IsPathRooted(filePath) || !Path.IsPathRooted(rootDirectory))
        {
            return null;
        }

        string? directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(directory))
        {
            return null;
        }

        if (directory.StartsWith(rootDirectory, StringComparison.OrdinalIgnoreCase))
        {
            string relativePath = Path.GetRelativePath(rootDirectory, directory);
            return relativePath == "." ? null : relativePath;
        }

        return null;
    }

}
