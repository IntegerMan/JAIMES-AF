namespace MattEland.Jaimes.ServiceDefinitions.Services;

public interface IDocumentCrackingService
{
    Task ProcessDocumentAsync(string filePath,
        string? relativeDirectory,
        string rulesetId,
        string documentKind,
        CancellationToken cancellationToken = default);
}