namespace MattEland.Jaimes.Workers.DocumentCrackerWorker.Services;

public interface IDocumentCrackingService
{
    Task ProcessDocumentAsync(string filePath, string? relativeDirectory, string? documentType = null, string? rulesetId = null, CancellationToken cancellationToken = default);
}

