namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Information about a single document chunk.
/// </summary>
public class DocumentChunkInfo
{
    /// <summary>
    /// Gets or sets the unique chunk ID (GUID-based string identifier).
    /// </summary>
    public string ChunkId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the index of this chunk within the document (0-based).
    /// </summary>
    public int ChunkIndex { get; set; }

    /// <summary>
    /// Gets or sets a preview of the chunk text (truncated).
    /// </summary>
    public string ChunkTextPreview { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this chunk has an embedding.
    /// </summary>
    public bool HasEmbedding { get; set; }

    /// <summary>
    /// Gets or sets when this chunk was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Information about a transcript message acting as a chunk.
/// </summary>
public class TranscriptChunkInfo
{
    /// <summary>
    /// Gets or sets the message ID.
    /// </summary>
    public int MessageId { get; set; }

    /// <summary>
    /// Gets or sets the message index in the conversation.
    /// </summary>
    public int MessageIndex { get; set; }

    /// <summary>
    /// Gets or sets a preview of the message text (truncated).
    /// </summary>
    public string MessageTextPreview { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this message has an embedding.
    /// </summary>
    public bool HasEmbedding { get; set; }

    /// <summary>
    /// Gets or sets the message role (User/Assistant).
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when this message was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Response containing paginated document chunks.
/// </summary>
public class DocumentChunksResponse
{
    /// <summary>
    /// Gets or sets the document ID.
    /// </summary>
    public int DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the document name.
    /// </summary>
    public string DocumentName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the document kind (e.g., "Sourcebook").
    /// </summary>
    public string DocumentKind { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ruleset ID.
    /// </summary>
    public string RulesetId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the chunks.
    /// </summary>
    public DocumentChunkInfo[] Chunks { get; set; } = [];

    /// <summary>
    /// Gets or sets the total count of chunks.
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
}

/// <summary>
/// Response containing paginated transcript chunks (messages).
/// </summary>
public class TranscriptChunksResponse
{
    /// <summary>
    /// Gets or sets the game ID.
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Gets or sets the game title.
    /// </summary>
    public string GameTitle { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the chunks (messages).
    /// </summary>
    public TranscriptChunkInfo[] Chunks { get; set; } = [];

    /// <summary>
    /// Gets or sets the total count of messages.
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
}
