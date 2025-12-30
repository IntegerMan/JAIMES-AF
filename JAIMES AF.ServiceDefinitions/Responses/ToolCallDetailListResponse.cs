namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Response containing a paginated list of tool call details.
/// </summary>
public class ToolCallDetailListResponse
{
    /// <summary>
    /// The list of tool call details.
    /// </summary>
    public IEnumerable<ToolCallDetailDto> Items { get; set; } = [];

    /// <summary>
    /// Total number of tool calls.
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
    /// The name of the tool being queried.
    /// </summary>
    public string? ToolName { get; set; }

    /// <summary>
    /// Description of the tool from the registry.
    /// </summary>
    public string? ToolDescription { get; set; }
}
