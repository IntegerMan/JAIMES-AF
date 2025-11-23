namespace MattEland.Jaimes.Workers.DocumentScanner.Services;

public interface IDocumentScannerService
{
    Task<DocumentScanSummary> ScanAndEnqueueAsync(string contentDirectory, CancellationToken cancellationToken = default);
}

public class DocumentScanSummary
{
    public int FilesScanned { get; set; }
    public int FilesEnqueued { get; set; }
    public int FilesUnchanged { get; set; }
    public int Errors { get; set; }
}




