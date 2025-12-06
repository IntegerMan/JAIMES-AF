namespace MattEland.Jaimes.Workers.DocumentChangeDetector.Services;

public class DocumentChangeDetectorBackgroundService(
    ILogger<DocumentChangeDetectorBackgroundService> logger,
    IDocumentChangeDetectorService documentChangeDetectorService,
    IHostApplicationLifetime hostApplicationLifetime,
    DocumentChangeDetectorOptions options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Document Change Detector Background Service starting");

        bool success = false;

        try
        {
            if (string.IsNullOrWhiteSpace(options.ContentDirectory))
            {
                logger.LogError("ContentDirectory is not configured. Document scanning will not run.");
                Environment.ExitCode = 1;
                return;
            }

            logger.LogInformation("Starting document scan of directory: {ContentDirectory}", options.ContentDirectory);

            DocumentScanSummary summary = await documentChangeDetectorService.ScanAndEnqueueAsync(
                options.ContentDirectory,
                stoppingToken);

            // Determine success: no errors occurred during scanning
            success = summary.Errors == 0;

            logger.LogInformation(
                "Document scan completed. Scanned: {FilesScanned}, Enqueued: {FilesEnqueued}, Unchanged: {FilesUnchanged}, Errors: {Errors}",
                summary.FilesScanned,
                summary.FilesEnqueued,
                summary.FilesUnchanged,
                summary.Errors);

            if (success)
                logger.LogInformation("Document scan completed successfully with no errors.");
            else
                logger.LogWarning("Document scan completed with {ErrorCount} error(s).", summary.Errors);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fatal error during document scanning");
            success = false;
        }
        finally
        {
            // Set exit code: 0 for success, 1 for failure
            Environment.ExitCode = success ? 0 : 1;

            logger.LogInformation(
                "Document Change Detector Background Service completed. Exit code: {ExitCode} ({Status})",
                Environment.ExitCode,
                success ? "Success" : "Failure");

            hostApplicationLifetime.StopApplication();
        }
    }
}