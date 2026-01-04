namespace MattEland.Jaimes.Workers.DocumentChangeDetector.Services;

public class DocumentChangeDetectorService(
    ILogger<DocumentChangeDetectorService> logger,
    IDirectoryScanner directoryScanner,
    IChangeTracker changeTracker,
    IDbContextFactory<JaimesDbContext> dbContextFactory,
    IMessagePublisher messagePublisher,
    ActivitySource activitySource,
    DocumentChangeDetectorOptions options) : IDocumentChangeDetectorService
{
    public async Task<DocumentScanSummary> ScanAndEnqueueAsync(string contentDirectory,
        CancellationToken cancellationToken = default)
    {
        DocumentScanSummary summary = new();

        if (string.IsNullOrWhiteSpace(contentDirectory))
            throw new InvalidOperationException("ContentDirectory is required for document scanning.");

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
            summary.FilesScanned,
            summary.FilesEnqueued,
            summary.FilesUnchanged,
            summary.Errors);

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
            if (cancellationToken.IsCancellationRequested) break;

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
                    // File hash matches - check if document was successfully cracked and has stored file
                    var (isComplete, reenqueueReason) = await CheckDocumentStatusAsync(filePath, cancellationToken);

                    if (isComplete)
                    {
                        // File unchanged and fully processed, update last scanned time
                        string? relativeDirectory = GetRelativeDirectory(filePath, rootDirectory);
                        await UpdateMetadataAsync(filePath, currentHash, relativeDirectory, cancellationToken);
                        summary.FilesUnchanged++;
                        logger.LogDebug("File unchanged and complete, skipping: {FilePath}", filePath);
                        fileActivity?.SetTag("scanner.status", "unchanged");
                    }
                    else if (reenqueueReason == "missing_stored_file")
                    {
                        // Document is cracked but missing stored file - upload directly without re-cracking
                        string? relativeDirectory = GetRelativeDirectory(filePath, rootDirectory);
                        bool uploaded = await UploadStoredFileForDocumentAsync(filePath, cancellationToken);
                        if (uploaded)
                        {
                            await UpdateMetadataAsync(filePath, currentHash, relativeDirectory, cancellationToken);
                            summary.FilesEnqueued++; // Count as processed
                            logger.LogInformation(
                                "Uploaded stored file for already-cracked document: {FilePath}",
                                filePath);
                            fileActivity?.SetTag("scanner.status", "uploaded_stored_file");
                        }
                        else
                        {
                            // If upload failed (e.g. file moved or db error), don't update metadata so we retry next time
                            summary.Errors++;
                            fileActivity?.SetTag("scanner.status", "upload_failed");
                        }
                    }
                    else
                    {
                        // File hash matches but document wasn't cracked - enqueue for cracking
                        string? relativeDirectory = GetRelativeDirectory(filePath, rootDirectory);
                        await EnqueueDocumentAsync(filePath, relativeDirectory, cancellationToken);
                        await UpdateMetadataAsync(filePath, currentHash, relativeDirectory, cancellationToken);
                        summary.FilesEnqueued++;
                        logger.LogInformation(
                            "File hash unchanged but not cracked, enqueuing for retry: {FilePath}",
                            filePath);
                        fileActivity?.SetTag("scanner.status", "retry_not_cracked");
                    }
                }
                else
                {
                    // File is new or changed, enqueue for processing
                    string? relativeDirectory = GetRelativeDirectory(filePath, rootDirectory);
                    await EnqueueDocumentAsync(filePath, relativeDirectory, cancellationToken);
                    await UpdateMetadataAsync(filePath, currentHash, relativeDirectory, cancellationToken);
                    summary.FilesEnqueued++;
                    logger.LogInformation("File enqueued for processing: {FilePath} (Hash: {Hash})",
                        filePath,
                        currentHash);
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
        await using JaimesDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        string lowPath = filePath.ToLowerInvariant();
        return await dbContext.DocumentMetadata
            .FirstOrDefaultAsync(x => x.FilePath.ToLower() == lowPath, cancellationToken);
    }

    private async Task<(bool IsComplete, string? ReenqueueReason)> CheckDocumentStatusAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        await using JaimesDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        string lowPath = filePath.ToLowerInvariant();
        CrackedDocument? crackedDocument = await dbContext.CrackedDocuments
            .FirstOrDefaultAsync(x => x.FilePath.ToLower() == lowPath, cancellationToken);

        // Not cracked at all
        if (crackedDocument == null || string.IsNullOrWhiteSpace(crackedDocument.Content))
        {
            return (false, "not_cracked");
        }

        // Check if upload is enabled and document is missing its stored file
        if (options.UploadDocumentsWhenCracking && !crackedDocument.StoredFileId.HasValue)
        {
            return (false, "missing_stored_file");
        }

        return (true, null);
    }

    private async Task UpdateMetadataAsync(
        string filePath,
        string hash,
        string? relativeDirectory,
        CancellationToken cancellationToken)
    {
        await using JaimesDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        string rulesetId = DocumentMetadataExtractor.ExtractRulesetId(relativeDirectory);
        string documentKind = DocumentMetadataExtractor.DetermineDocumentKind(relativeDirectory);

        string lowPath = filePath.ToLowerInvariant();
        DocumentMetadata? existingMetadata = await dbContext.DocumentMetadata
            .FirstOrDefaultAsync(x => x.FilePath.ToLower() == lowPath, cancellationToken);

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

    private async Task<bool> UploadStoredFileForDocumentAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        await using JaimesDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        string lowPath = filePath.ToLowerInvariant();
        // Find the cracked document
        CrackedDocument? crackedDocument = await dbContext.CrackedDocuments
            .FirstOrDefaultAsync(x => x.FilePath.ToLower() == lowPath, cancellationToken);

        if (crackedDocument == null)
        {
            logger.LogWarning("Cannot upload stored file: CrackedDocument not found for {FilePath}", filePath);
            return false;
        }

        if (crackedDocument.StoredFileId.HasValue)
        {
            logger.LogDebug("Document already has a stored file, skipping upload: {FilePath}", filePath);
            return true;
        }

        try
        {
            FileInfo fileInfo = new(filePath);
            if (!fileInfo.Exists)
            {
                logger.LogWarning("Cannot upload stored file: File not found at {FilePath}", filePath);
                return false;
            }

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
            crackedDocument.StoredFile = storedFile;

            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Successfully uploaded stored file for document: {FilePath} ({Size} bytes)",
                filePath, fileInfo.Length);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to upload stored file for document: {FilePath}", filePath);
            return false;
        }
    }

    private static string? GetRelativeDirectory(string filePath, string rootDirectory)
    {
        if (!Path.IsPathRooted(filePath) || !Path.IsPathRooted(rootDirectory)) return null;

        string? directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(directory)) return null;

        if (directory.StartsWith(rootDirectory, StringComparison.OrdinalIgnoreCase))
        {
            string relativePath = Path.GetRelativePath(rootDirectory, directory);
            return relativePath == "." ? null : relativePath;
        }

        return null;
    }
}