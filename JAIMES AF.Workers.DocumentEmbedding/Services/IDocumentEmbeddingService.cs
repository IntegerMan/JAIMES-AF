namespace MattEland.Jaimes.Workers.DocumentEmbedding.Services;

public interface IDocumentEmbeddingService
{
    Task ProcessChunkAsync(ChunkReadyForEmbeddingMessage message, CancellationToken cancellationToken = default);
}