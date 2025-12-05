using MattEland.Jaimes.ServiceDefinitions.Messages;

namespace MattEland.Jaimes.ServiceDefinitions.Services;

public interface IDocumentChunkingService
{
    Task ProcessDocumentAsync(DocumentReadyForChunkingMessage message, CancellationToken cancellationToken = default);
}
