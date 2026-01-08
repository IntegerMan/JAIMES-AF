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
    /// Count of positive sentiment records (sentiment = <see cref="MattEland.Jaimes.Domain.SentimentValue.Positive"/>).
    /// </summary>
    public int PositiveCount { get; init; }

    /// <summary>
    /// Count of neutral sentiment records (sentiment = <see cref="MattEland.Jaimes.Domain.SentimentValue.Neutral"/>).
    /// </summary>
    public int NeutralCount { get; init; }

    /// <summary>
    /// Count of negative sentiment records (sentiment = <see cref="MattEland.Jaimes.Domain.SentimentValue.Negative"/>).
    /// </summary>
    public int NegativeCount { get; init; }

    /// <summary>
    /// Average confidence across the filtered set (0.0 to 1.0).
    /// </summary>
    public double? AverageConfidence { get; init; }
}
