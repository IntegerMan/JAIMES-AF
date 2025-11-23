namespace MattEland.Jaimes.Workers.DocumentEmbeddings.Services;

public interface IQdrantEmbeddingStore
{
    Task StoreEmbeddingAsync(string documentId, float[] embedding, Dictionary<string, string> metadata, CancellationToken cancellationToken = default);
    Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default);
}

