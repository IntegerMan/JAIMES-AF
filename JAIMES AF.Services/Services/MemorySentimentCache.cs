using System.Collections.Concurrent;
using MattEland.Jaimes.ServiceDefinitions.Models;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.Extensions.Logging;

namespace MattEland.Jaimes.Services.Services;

/// <summary>
/// In-memory cache for pending sentiment classification results.
/// Uses ConcurrentDictionary with TTL expiration and background cleanup.
/// </summary>
public class MemorySentimentCache : IPendingSentimentCache, IDisposable
{
    private readonly ConcurrentDictionary<Guid, CachedSentimentResult> _cache = new();
    private readonly TimeSpan _ttl = TimeSpan.FromMinutes(5);
    private readonly Timer _cleanupTimer;
    private readonly ILogger<MemorySentimentCache> _logger;

    public MemorySentimentCache(ILogger<MemorySentimentCache> logger)
    {
        _logger = logger;

        // Background cleanup every 1 minute
        _cleanupTimer = new Timer(CleanupExpiredEntries, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

        _logger.LogInformation("MemorySentimentCache initialized with TTL: {TTL}", _ttl);
    }

    /// <inheritdoc />
    public void Store(Guid correlationToken, int sentiment, double confidence)
    {
        var result = new CachedSentimentResult
        {
            Sentiment = sentiment,
            Confidence = confidence,
            CachedAt = DateTime.UtcNow
        };

        _cache[correlationToken] = result;

        _logger.LogDebug("Stored sentiment for correlation token {Token}: {Sentiment} (confidence: {Confidence:P0})",
            correlationToken, sentiment, confidence);
    }

    /// <inheritdoc />
    public bool TryGet(Guid correlationToken, out CachedSentimentResult? result)
    {
        if (_cache.TryGetValue(correlationToken, out var cachedResult))
        {
            // Check if expired
            if (DateTime.UtcNow - cachedResult.CachedAt < _ttl)
            {
                result = cachedResult;
                _logger.LogDebug("Cache hit for correlation token {Token}: {Sentiment} (age: {Age})",
                    correlationToken, cachedResult.Sentiment, DateTime.UtcNow - cachedResult.CachedAt);
                return true;
            }

            // Expired - remove it
            _cache.TryRemove(correlationToken, out _);
            _logger.LogWarning("Correlation token {Token} expired (age: {Age}, TTL: {TTL})",
                correlationToken, DateTime.UtcNow - cachedResult.CachedAt, _ttl);
        }

        result = null;
        _logger.LogDebug("Cache miss for correlation token {Token}", correlationToken);
        return false;
    }

    /// <inheritdoc />
    public void Remove(Guid correlationToken)
    {
        if (_cache.TryRemove(correlationToken, out _))
        {
            _logger.LogDebug("Removed correlation token {Token} from cache", correlationToken);
        }
    }

    private void CleanupExpiredEntries(object? state)
    {
        var now = DateTime.UtcNow;
        var expiredKeys = _cache
            .Where(kvp => now - kvp.Value.CachedAt >= _ttl)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _cache.TryRemove(key, out _);
        }

        if (expiredKeys.Count > 0)
        {
            _logger.LogInformation("Cleaned up {Count} expired sentiment cache entries", expiredKeys.Count);
        }

        _logger.LogDebug("Sentiment cache size: {Size} entries", _cache.Count);
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        _cache.Clear();
    }
}
