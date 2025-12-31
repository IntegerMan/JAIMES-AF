namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Response containing a paginated list of evaluators.
/// </summary>
public class EvaluatorListResponse
{
    /// <summary>
    /// Gets or sets the list of evaluator items.
    /// </summary>
    public List<EvaluatorItemDto> Items { get; set; } = [];

    /// <summary>
    /// Gets or sets the total count of evaluators (for pagination).
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Gets or sets the current page number (1-based).
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    public int PageSize { get; set; }
}
