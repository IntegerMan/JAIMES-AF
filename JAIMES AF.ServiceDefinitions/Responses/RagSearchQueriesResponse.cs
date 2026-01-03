namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Information about a single result chunk from a search query.
/// </summary>
public class RagSearchResultInfo
{
    /// <summary>
    /// Gets or sets the document name.
    /// </summary>
    public string DocumentName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the chunk ID.
    /// </summary>
    public string ChunkId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the relevancy score (0.0 to 1.0).
    /// </summary>
    public double Relevancy { get; set; }

    /// <summary>
    /// Gets or sets the document ID.
    /// </summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ruleset ID.
    /// </summary>
    public string RulesetId { get; set; } = string.Empty;
}

/// <summary>
/// Information about a single RAG search query.
/// </summary>
public class RagSearchQueryInfo
{
    /// <summary>
    /// Gets or sets the query ID.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the query text.
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the query was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the number of results returned.
    /// </summary>
    public int ResultCount { get; set; }

    /// <summary>
    /// Gets or sets the ruleset ID filter used (if any).
    /// </summary>
    public string? RulesetId { get; set; }

    /// <summary>
    /// Gets or sets the filter JSON used (if any).
    /// </summary>
    public string? FilterJson { get; set; }

    /// <summary>
    /// Gets or sets the result chunks.
    /// </summary>
    public RagSearchResultInfo[] Results { get; set; } = [];
}

/// <summary>
/// Response containing paginated RAG search queries for a collection.
/// </summary>
public class RagSearchQueriesResponse
{
    /// <summary>
    /// Gets or sets the index name being queried.
    /// </summary>
    public string IndexName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name for the collection.
    /// </summary>
    public string CollectionDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total number of queries.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Gets or sets the current page number.
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Gets or sets the queries.
    /// </summary>
    public RagSearchQueryInfo[] Queries { get; set; } = [];
}
