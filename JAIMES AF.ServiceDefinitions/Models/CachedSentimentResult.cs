namespace MattEland.Jaimes.ServiceDefinitions.Models;

/// <summary>
/// Represents a sentiment classification result cached in memory before message persistence.
/// </summary>
public class CachedSentimentResult
{
    /// <summary>
    /// The sentiment value: -1 (negative), 0 (neutral), or 1 (positive).
    /// </summary>
    public int Sentiment { get; init; }

    /// <summary>
    /// The confidence score of the sentiment prediction (0.0 to 1.0).
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// When this result was cached.
    /// </summary>
    public DateTime CachedAt { get; init; }
}
