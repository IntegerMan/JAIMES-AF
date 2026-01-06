namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Response containing sentiment analysis summary statistics.
/// </summary>
public record SentimentSummaryResponse
{
    /// <summary>
    /// Total count of all sentiment records.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Count of positive sentiment records (sentiment = 1).
    /// </summary>
    public int PositiveCount { get; init; }

    /// <summary>
    /// Count of neutral sentiment records (sentiment = 0).
    /// </summary>
    public int NeutralCount { get; init; }

    /// <summary>
    /// Count of negative sentiment records (sentiment = -1).
    /// </summary>
    public int NegativeCount { get; init; }
}
