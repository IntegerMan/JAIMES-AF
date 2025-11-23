using System.Diagnostics;
using MassTransit;
using MattEland.Jaimes.DocumentProcessing.Services;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Models;
using MattEland.Jaimes.Workers.DocumentChangeDetector.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace MattEland.Jaimes.Workers.DocumentChangeDetector.Services;

public class DocumentChangeDetectorService(
    ILogger<DocumentChangeDetectorService> logger,
    IDirectoryScanner directoryScanner,
    IChangeTracker changeTracker,
    IMongoClient mongoClient,
    IPublishEndpoint publishEndpoint,
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

        // Get MongoDB collections
        IMongoDatabase database = mongoClient.GetDatabase("documents");
        IMongoCollection<DocumentMetadata> metadataCollection = database.GetCollection<DocumentMetadata>("documentMetadata");
        IMongoCollection<CrackedDocument> crackedCollection = database.GetCollection<CrackedDocument>("crackedDocuments");

        // Ensure index on FilePath for fast lookups
        await metadataCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<DocumentMetadata>(
                Builders<DocumentMetadata>.IndexKeys.Ascending(x => x.FilePath),
                new CreateIndexOptions { Unique = true }),
            cancellationToken: cancellationToken);

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

            await ProcessDirectoryAsync(directory, contentDirectory, metadataCollection, crackedCollection, summary, cancellationToken);
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
        IMongoCollection<DocumentMetadata> metadataCollection,
        IMongoCollection<CrackedDocument> crackedCollection,
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
                DocumentMetadata? storedMetadata = await GetStoredMetadataAsync(metadataCollection, filePath, cancellationToken);

                // Check if file has changed
                if (storedMetadata != null && storedMetadata.Hash == currentHash)
                {
                    // File hash matches - check if document was successfully cracked
                    bool isCracked = await IsDocumentCrackedAsync(crackedCollection, filePath, cancellationToken);
                    
                    if (isCracked)
                    {
                        // File unchanged and successfully cracked, update last scanned time
                        await UpdateMetadataAsync(metadataCollection, filePath, currentHash, cancellationToken);
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
                        await UpdateMetadataAsync(metadataCollection, filePath, currentHash, cancellationToken);
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
                    await UpdateMetadataAsync(metadataCollection, filePath, currentHash, cancellationToken);
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
        IMongoCollection<DocumentMetadata> collection,
        string filePath,
        CancellationToken cancellationToken)
    {
        FilterDefinition<DocumentMetadata> filter = Builders<DocumentMetadata>.Filter.Eq(x => x.FilePath, filePath);
        return await collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<bool> IsDocumentCrackedAsync(
        IMongoCollection<CrackedDocument> collection,
        string filePath,
        CancellationToken cancellationToken)
    {
        FilterDefinition<CrackedDocument> filter = Builders<CrackedDocument>.Filter.Eq(x => x.FilePath, filePath);
        CrackedDocument? crackedDocument = await collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
        return crackedDocument != null && !string.IsNullOrWhiteSpace(crackedDocument.Content);
    }

    private async Task UpdateMetadataAsync(
        IMongoCollection<DocumentMetadata> metadataCollection,
        string filePath,
        string hash,
        CancellationToken cancellationToken)
    {
        FilterDefinition<DocumentMetadata> filter = Builders<DocumentMetadata>.Filter.Eq(x => x.FilePath, filePath);
        UpdateDefinition<DocumentMetadata> update = Builders<DocumentMetadata>.Update
            .Set(x => x.Hash, hash)
            .Set(x => x.LastScanned, DateTime.UtcNow);

        UpdateOptions options = new() { IsUpsert = true };

        await metadataCollection.UpdateOneAsync(filter, update, options, cancellationToken);
    }

    private async Task EnqueueDocumentAsync(
        string filePath,
        string? relativeDirectory,
        CancellationToken cancellationToken)
    {
        CrackDocumentMessage message = new()
        {
            FilePath = filePath,
            RelativeDirectory = relativeDirectory
        };

        await publishEndpoint.Publish(message, cancellationToken);
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

