using MattEland.Jaimes.ServiceDefinitions.Messages;

namespace MattEland.Jaimes.Workers.DocumentChunking.Services;

public interface IDocumentChunkingService
{
    Task ProcessDocumentAsync(DocumentCrackedMessage message, CancellationToken cancellationToken = default);
}

