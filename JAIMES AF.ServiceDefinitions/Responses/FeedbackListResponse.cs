namespace MattEland.Jaimes.ServiceDefinitions.Responses;

public class FeedbackListResponse
{
    public IEnumerable<FeedbackListItemDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// Response containing feedback summary statistics.
/// </summary>
public record FeedbackSummaryResponse
{
    /// <summary>
    /// Total count of all feedback records.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Count of positive feedback records.
    /// </summary>
    public int PositiveCount { get; init; }

    /// <summary>
    /// Count of negative feedback records.
    /// </summary>
    public int NegativeCount { get; init; }

    /// <summary>
    /// Count of feedback records with comments.
    /// </summary>
    public int WithCommentsCount { get; init; }
}
