namespace MattEland.Jaimes.DocumentProcessing.Services;

public class ChangeTracker(ILogger<ChangeTracker> logger) : IChangeTracker
{
    public async Task<string> ComputeFileHashAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException($"File not found: {filePath}", filePath);

        using SHA256 sha256 = SHA256.Create();
        await using FileStream fileStream = File.OpenRead(filePath);

        byte[] hashBytes = await sha256.ComputeHashAsync(fileStream, cancellationToken);
        string hash = Convert.ToHexString(hashBytes);

        logger.LogDebug("Computed hash for file: {FilePath} -> {Hash}", filePath, hash);

        return hash;
    }
}