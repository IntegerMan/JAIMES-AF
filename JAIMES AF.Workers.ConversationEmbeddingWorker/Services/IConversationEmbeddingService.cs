namespace MattEland.Jaimes.Workers.ConversationEmbedding.Services;

public interface IConversationEmbeddingService
{
    Task ProcessConversationMessageAsync(ConversationMessageReadyForEmbeddingMessage message, CancellationToken cancellationToken = default);
}

