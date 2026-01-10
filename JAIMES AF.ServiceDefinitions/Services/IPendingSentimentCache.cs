using MattEland.Jaimes.ServiceDefinitions.Models;

namespace MattEland.Jaimes.ServiceDefinitions.Services;

/// <summary>
/// Cache for storing sentiment classification results before message persistence.
/// Used to correlate early sentiment classification with database message IDs.
/// </summary>
public interface IPendingSentimentCache
{
    /// <summary>
    /// Stores a sentiment classification result associated with a correlation token.
    /// </summary>
    /// <param name="correlationToken">Unique identifier to correlate with the message later.</param>
    /// <param name="sentiment">The sentiment value (-1, 0, or 1).</param>
    /// <param name="confidence">The confidence score (0.0 to 1.0).</param>
    void Store(Guid correlationToken, int sentiment, double confidence);

    /// <summary>
    /// Attempts to retrieve a cached sentiment result by correlation token.
    /// </summary>
    /// <param name="correlationToken">The correlation token to look up.</param>
    /// <param name="result">The cached result if found, null otherwise.</param>
    /// <returns>True if the result was found and not expired, false otherwise.</returns>
    bool TryGet(Guid correlationToken, out CachedSentimentResult? result);

    /// <summary>
    /// Removes a cached result by correlation token.
    /// </summary>
    /// <param name="correlationToken">The correlation token to remove.</param>
    void Remove(Guid correlationToken);
}
