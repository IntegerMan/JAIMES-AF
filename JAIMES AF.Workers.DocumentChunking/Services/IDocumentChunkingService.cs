using MattEland.Jaimes.ServiceDefinitions.Messages;

namespace MattEland.Jaimes.Workers.DocumentChunking.Services;

public interface IDocumentChunkingService
{
    Task ProcessDocumentAsync(DocumentReadyForChunkingMessage message, CancellationToken cancellationToken = default);
}

