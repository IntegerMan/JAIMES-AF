namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Information about a query that returned a specific chunk.
/// </summary>
public class ChunkQueryAppearance
{
    /// <summary>
    /// Gets or sets the query ID.
    /// </summary>
    public Guid QueryId { get; set; }

    /// <summary>
    /// Gets or sets the query text.
    /// </summary>
    public string QueryText { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the relevancy score for this chunk in the query.
    /// </summary>
    public double Relevancy { get; set; }

    /// <summary>
    /// Gets or sets when the query was executed.
    /// </summary>
    public DateTime QueryDate { get; set; }
}

/// <summary>
/// Response containing detailed information about a single document chunk.
/// </summary>
public class ChunkDetailsResponse
{
    /// <summary>
    /// Gets or sets the unique chunk ID.
    /// </summary>
    public string ChunkId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the index of this chunk within the document.
    /// </summary>
    public int ChunkIndex { get; set; }

    /// <summary>
    /// Gets or sets the full chunk text.
    /// </summary>
    public string ChunkText { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this chunk has an embedding.
    /// </summary>
    public bool HasEmbedding { get; set; }

    /// <summary>
    /// Gets or sets the Qdrant point ID (if embedded).
    /// </summary>
    public string? QdrantPointId { get; set; }

    /// <summary>
    /// Gets or sets a preview of the embedding vector (first N dimensions).
    /// </summary>
    public float[]? EmbeddingPreview { get; set; }

    /// <summary>
    /// Gets or sets the full embedding vector (for demonstration purposes).
    /// </summary>
    public float[]? FullEmbedding { get; set; }

    /// <summary>
    /// Gets or sets the total embedding dimensions.
    /// </summary>
    public int EmbeddingDimensions { get; set; }

    /// <summary>
    /// Gets or sets when this chunk was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    // Document context

    /// <summary>
    /// Gets or sets the document ID.
    /// </summary>
    public int DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the document name.
    /// </summary>
    public string DocumentName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the document kind.
    /// </summary>
    public string DocumentKind { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ruleset ID.
    /// </summary>
    public string RulesetId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the queries that returned this chunk.
    /// </summary>
    public ChunkQueryAppearance[] QueryAppearances { get; set; } = [];
}

/// <summary>
/// Response containing detailed information about a transcript message.
/// </summary>
public class TranscriptMessageDetailsResponse
{
    /// <summary>
    /// Gets or sets the message ID.
    /// </summary>
    public int MessageId { get; set; }

    /// <summary>
    /// Gets or sets the full message text.
    /// </summary>
    public string MessageText { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the message role.
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this message has an embedding.
    /// </summary>
    public bool HasEmbedding { get; set; }

    /// <summary>
    /// Gets or sets the Qdrant point ID (if embedded).
    /// </summary>
    public string? QdrantPointId { get; set; }

    /// <summary>
    /// Gets or sets a preview of the embedding vector.
    /// </summary>
    public float[]? EmbeddingPreview { get; set; }

    /// <summary>
    /// Gets or sets the full embedding vector (for demonstration purposes).
    /// </summary>
    public float[]? FullEmbedding { get; set; }

    /// <summary>
    /// Gets or sets the total embedding dimensions.
    /// </summary>
    public int EmbeddingDimensions { get; set; }

    /// <summary>
    /// Gets or sets when this message was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    // Game context

    /// <summary>
    /// Gets or sets the game ID.
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Gets or sets the game title.
    /// </summary>
    public string GameTitle { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the queries that returned this message.
    /// </summary>
    public ChunkQueryAppearance[] QueryAppearances { get; set; } = [];
}
