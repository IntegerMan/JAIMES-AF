namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Information about a single document in a RAG collection.
/// </summary>
public class RagCollectionDocumentInfo
{
    /// <summary>
    /// Gets or sets the unique document identifier.
    /// </summary>
    public int DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the document file name.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the relative directory path.
    /// </summary>
    public string RelativeDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the document kind (e.g., "Sourcebook", "Transcript").
    /// </summary>
    public string DocumentKind { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ruleset ID this document belongs to.
    /// </summary>
    public string RulesetId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total number of chunks for this document.
    /// </summary>
    public int TotalChunks { get; set; }

    /// <summary>
    /// Gets or sets the number of chunks that have embeddings.
    /// </summary>
    public int EmbeddedChunks { get; set; }

    /// <summary>
    /// Gets or sets whether this document is fully processed.
    /// </summary>
    public bool IsFullyProcessed { get; set; }

    /// <summary>
    /// Gets or sets when the document was cracked.
    /// </summary>
    public DateTime CrackedAt { get; set; }
}

/// <summary>
/// Summary statistics for a collection type.
/// </summary>
public class RagCollectionSummary
{
    /// <summary>
    /// Gets or sets the collection type (e.g., "Sourcebook", "Transcript").
    /// </summary>
    public string CollectionType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total number of documents.
    /// </summary>
    public int DocumentCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of chunks.
    /// </summary>
    public int TotalChunks { get; set; }

    /// <summary>
    /// Gets or sets the total number of embedded chunks.
    /// </summary>
    public int EmbeddedChunks { get; set; }

    /// <summary>
    /// Gets or sets the total number of queries against this collection.
    /// </summary>
    public int QueryCount { get; set; }
}

/// <summary>
/// Response containing RAG collection statistics.
/// </summary>
public class RagCollectionStatisticsResponse
{
    /// <summary>
    /// Gets or sets the collection summaries by type.
    /// </summary>
    public RagCollectionSummary[] Summaries { get; set; } = [];

    /// <summary>
    /// Gets or sets the individual document information.
    /// </summary>
    public RagCollectionDocumentInfo[] Documents { get; set; } = [];
}
