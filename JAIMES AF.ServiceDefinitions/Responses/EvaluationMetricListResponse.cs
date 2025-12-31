namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Paginated response for evaluation metrics list.
/// </summary>
public class EvaluationMetricListResponse
{
    /// <summary>
    /// The list of evaluation metric items.
    /// </summary>
    public IEnumerable<EvaluationMetricListItemDto> Items { get; set; } = [];

    /// <summary>
    /// Total count of items matching the filter criteria.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Current page number (1-based).
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Number of items per page.
    /// </summary>
    public int PageSize { get; set; }
}
