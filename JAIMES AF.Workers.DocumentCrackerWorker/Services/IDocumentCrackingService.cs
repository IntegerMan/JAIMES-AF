namespace MattEland.Jaimes.Workers.DocumentCrackerWorker.Services;

public interface IDocumentCrackingService
{
    Task ProcessDocumentAsync(string filePath, string? relativeDirectory, CancellationToken cancellationToken = default);
}

