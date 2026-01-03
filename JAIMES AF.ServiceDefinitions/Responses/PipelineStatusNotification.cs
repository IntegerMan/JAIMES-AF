namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Notification sent when document pipeline queue sizes change.
/// Used for real-time SignalR updates to web clients.
/// </summary>
public record PipelineStatusNotification
{
    /// <summary>
    /// Number of documents waiting to be cracked (text extracted).
    /// </summary>
    public int CrackingQueueSize { get; init; }

    /// <summary>
    /// Number of documents waiting to be chunked.
    /// </summary>
    public int ChunkingQueueSize { get; init; }

    /// <summary>
    /// Number of chunks waiting to be embedded.
    /// </summary>
    public int EmbeddingQueueSize { get; init; }

    /// <summary>
    /// Total number of documents that are fully processed (ready).
    /// </summary>
    public int ReadyCount { get; init; }

    /// <summary>
    /// Total number of chunks across all documents.
    /// </summary>
    public int TotalChunks { get; init; }

    /// <summary>
    /// Total number of embeddings (chunks with QdrantPointId).
    /// </summary>
    public int TotalEmbeddings { get; init; }

    /// <summary>
    /// Timestamp when this status was captured.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Optional source identifier for the worker reporting this status.
    /// </summary>
    public string? WorkerSource { get; init; }
}
