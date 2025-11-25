using MattEland.Jaimes.ServiceDefinitions.Messages;

namespace MattEland.Jaimes.Workers.DocumentEmbeddings.Services;

public interface IDocumentEmbeddingService
{
    Task ProcessChunkAsync(ChunkReadyForEmbeddingMessage message, CancellationToken cancellationToken = default);
}

