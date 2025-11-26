namespace MattEland.Jaimes.Workers.DocumentChunking.Services;

public interface IQdrantEmbeddingStore
{
    Task StoreEmbeddingAsync(string pointId, float[] embedding, Dictionary<string, string> metadata, CancellationToken cancellationToken = default);
    Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default);
    Task<List<EmbeddingInfo>> ListEmbeddingsAsync(CancellationToken cancellationToken = default);
    Task DeleteEmbeddingAsync(string pointId, CancellationToken cancellationToken = default);
    Task DeleteAllEmbeddingsAsync(CancellationToken cancellationToken = default);
}

public record EmbeddingInfo
{
    public required string PointId { get; init; }
    public required ulong QdrantPointId { get; init; }
    public required string DocumentId { get; init; }
    public required string FileName { get; init; }
    public required string ChunkId { get; init; }
    public required int ChunkIndex { get; init; }
    public required string ChunkText { get; init; }
}



