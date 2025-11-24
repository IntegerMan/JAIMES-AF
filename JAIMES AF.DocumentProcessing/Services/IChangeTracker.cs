namespace MattEland.Jaimes.DocumentProcessing.Services;

public interface IChangeTracker
{
    Task<string> ComputeFileHashAsync(string filePath, CancellationToken cancellationToken = default);
}






