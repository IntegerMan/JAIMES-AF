namespace MattEland.Jaimes.Workers.ConversationEmbedding.Consumers;

public class ConversationMessageEmbeddingConsumer(
    IConversationEmbeddingService embeddingService,
    ILogger<ConversationMessageEmbeddingConsumer> logger,
    ActivitySource activitySource) : IMessageConsumer<ConversationMessageReadyForEmbeddingMessage>
{
    public async Task HandleAsync(ConversationMessageReadyForEmbeddingMessage message, CancellationToken cancellationToken = default)
    {
        using Activity? activity = activitySource.StartActivity("ConversationEmbedding.ConsumeMessage");
        activity?.SetTag("messaging.message_type", nameof(ConversationMessageReadyForEmbeddingMessage));
        activity?.SetTag("messaging.message_id", message.MessageId);
        activity?.SetTag("messaging.game_id", message.GameId.ToString());

        try
        {
            logger.LogDebug(
                "Received conversation message ready for embedding: MessageId={MessageId}, GameId={GameId}, Role={Role}",
                message.MessageId,
                message.GameId,
                message.Role);

            // Validate message
            if (message.MessageId <= 0)
            {
                logger.LogError(
                    "Received conversation message ready for embedding message with invalid MessageId. GameId={GameId}, Role={Role}. " +
                    "Skipping processing to avoid errors.",
                    message.GameId,
                    message.Role);
                activity?.SetStatus(ActivityStatusCode.Error, "Invalid MessageId");
                // Don't throw - just skip this message to avoid infinite retries
                return;
            }

            if (string.IsNullOrWhiteSpace(message.Text))
            {
                logger.LogError(
                    "Received conversation message ready for embedding message with empty Text. MessageId={MessageId}, GameId={GameId}. " +
                    "Skipping processing.",
                    message.MessageId,
                    message.GameId);
                activity?.SetStatus(ActivityStatusCode.Error, "Empty Text");
                return;
            }

            await embeddingService.ProcessConversationMessageAsync(message, cancellationToken);

            logger.LogDebug("Successfully processed conversation message embedding: {MessageId}", message.MessageId);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process conversation message ready for embedding message for {MessageId}", message.MessageId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            // Re-throw to let message consumer service handle retry logic
            throw;
        }
    }
}

