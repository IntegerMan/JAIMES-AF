using MattEland.Jaimes.ServiceDefinitions.Messages;

namespace MattEland.Jaimes.Workers.DocumentEmbeddings.Services;

public interface IDocumentEmbeddingService
{
    Task ProcessDocumentAsync(DocumentCrackedMessage message, CancellationToken cancellationToken = default);
}

