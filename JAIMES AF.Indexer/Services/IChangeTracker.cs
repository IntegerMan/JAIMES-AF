namespace MattEland.Jaimes.Indexer.Services;

public interface IChangeTracker
{
    Task<string> ComputeFileHashAsync(string filePath, CancellationToken cancellationToken = default);
}

