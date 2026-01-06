namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Response containing a paginated list of tool usage statistics.
/// </summary>
public class ToolUsageListResponse
{
    /// <summary>
    /// The list of tool usage statistics.
    /// </summary>
    public IEnumerable<ToolUsageItemDto> Items { get; set; } = [];

    /// <summary>
    /// Total number of distinct tools.
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

    /// <summary>
    /// Total number of tool calls across all tools.
    /// </summary>
    public int TotalCalls { get; set; }

    /// <summary>
    /// Total number of helpful feedback responses across all tools.
    /// </summary>
    public int TotalHelpful { get; set; }

    /// <summary>
    /// Total number of unhelpful feedback responses across all tools.
    /// </summary>
    public int TotalUnhelpful { get; set; }

    /// <summary>
    /// Average usage percentage across all tools.
    /// </summary>
    public double AverageUsagePercentage { get; set; }
}
