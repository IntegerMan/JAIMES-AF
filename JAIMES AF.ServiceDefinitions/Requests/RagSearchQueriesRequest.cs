namespace MattEland.Jaimes.ServiceDefinitions.Requests;

/// <summary>
/// Request for listing RAG search queries by collection index.
/// </summary>
public class RagSearchQueriesRequest
{
    /// <summary>
    /// Gets or sets the index name (e.g., "rules" or "conversations").
    /// </summary>
    public required string IndexName { get; set; }

    /// <summary>
    /// Gets or sets the page number (1-indexed).
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Gets or sets the number of items per page.
    /// </summary>
    public int PageSize { get; set; } = 25;
}
