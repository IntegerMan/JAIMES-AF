namespace MattEland.Jaimes.Indexer.Services;

public class DocumentState
{
    public required string FilePath { get; init; }
    public required string Hash { get; init; }
    public DateTime LastIndexed { get; init; }
}

public interface IChangeTracker
{
    Task<DocumentState?> GetDocumentStateAsync(string filePath, CancellationToken cancellationToken = default);
    Task SaveDocumentStateAsync(DocumentState state, CancellationToken cancellationToken = default);
    Task<string> ComputeFileHashAsync(string filePath, CancellationToken cancellationToken = default);
}

