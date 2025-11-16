using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace MattEland.Jaimes.Indexer.Services;

public class ChangeTracker : IChangeTracker
{
    private readonly ILogger<ChangeTracker> _logger;

    public ChangeTracker(ILogger<ChangeTracker> logger)
    {
        _logger = logger;
    }

    public async Task<string> ComputeFileHashAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await using FileStream stream = File.OpenRead(filePath);
        byte[] hashBytes = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToBase64String(hashBytes);
    }
}

