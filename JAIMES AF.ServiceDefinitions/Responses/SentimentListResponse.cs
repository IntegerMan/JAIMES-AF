namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Response containing a paginated list of sentiment records.
/// </summary>
public class SentimentListResponse
{
    /// <summary>
    /// Gets or sets the list of sentiment items for the current page.
    /// </summary>
    public IEnumerable<SentimentListItemDto> Items { get; set; } = [];

    /// <summary>
    /// Gets or sets the total count of sentiment records matching the filters.
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
