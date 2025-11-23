using MattEland.Jaimes.Workers.DocumentScanner.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MattEland.Jaimes.Workers.DocumentScanner.Services;

public class DocumentScannerBackgroundService(
    ILogger<DocumentScannerBackgroundService> logger,
    IDocumentScannerService documentScannerService,
    DocumentScannerOptions options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Document Scanner Background Service starting");

        try
        {
            if (string.IsNullOrWhiteSpace(options.ContentDirectory))
            {
                logger.LogError("ContentDirectory is not configured. Document scanning will not run.");
                return;
            }

            logger.LogInformation("Starting document scan of directory: {ContentDirectory}", options.ContentDirectory);

            DocumentScanSummary summary = await documentScannerService.ScanAndEnqueueAsync(
                options.ContentDirectory,
                stoppingToken);

            logger.LogInformation(
                "Document scan completed. Scanned: {FilesScanned}, Enqueued: {FilesEnqueued}, Unchanged: {FilesUnchanged}, Errors: {Errors}",
                summary.FilesScanned,
                summary.FilesEnqueued,
                summary.FilesUnchanged,
                summary.Errors);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fatal error during document scanning");
        }
        finally
        {
            logger.LogInformation("Document Scanner Background Service completed");
        }
    }
}

