using MattEland.Jaimes.ServiceDefinitions.Messages;

namespace MattEland.Jaimes.Workers.UserMessageWorker.Consumers;

public class UserMessageConsumer(
    IDbContextFactory<JaimesDbContext> contextFactory,
    IMessagePublisher messagePublisher,
    ILogger<UserMessageConsumer> logger,
    ActivitySource activitySource) : IMessageConsumer<ConversationMessageQueuedMessage>
{
    public async Task HandleAsync(ConversationMessageQueuedMessage message, CancellationToken cancellationToken = default)
    {
        using Activity? activity = activitySource.StartActivity("UserMessage.Process");
        activity?.SetTag("messaging.message_type", nameof(ConversationMessageQueuedMessage));
        activity?.SetTag("message.id", message.MessageId);
        activity?.SetTag("message.game_id", message.GameId.ToString());
        activity?.SetTag("message.role", message.Role.ToString());

        try
        {
            // Note: Role-based routing ensures only User messages reach this consumer
            // No need to filter by role here
            
            logger.LogInformation(
                "Processing user message: MessageId={MessageId}, GameId={GameId}",
                message.MessageId,
                message.GameId);

            // Load message from database
            await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
            Message? messageEntity = await context.Messages
                .Include(m => m.Game)
                .Include(m => m.Player)
                .FirstOrDefaultAsync(m => m.Id == message.MessageId, cancellationToken);

            if (messageEntity == null)
            {
                logger.LogWarning(
                    "Message {MessageId} not found in database. It may have been deleted.",
                    message.MessageId);
                activity?.SetStatus(ActivityStatusCode.Error, "Message not found");
                return;
            }

            // Set additional trace tags
            activity?.SetTag("message.text_length", messageEntity.Text?.Length ?? 0);
            activity?.SetTag("message.text", messageEntity.Text ?? "(empty)");
            activity?.SetTag("message.created_at", messageEntity.CreatedAt.ToString("O"));
            if (messageEntity.PlayerId != null)
            {
                activity?.SetTag("message.player_id", messageEntity.PlayerId);
            }

            // Log message details
            string textPreview = messageEntity.Text?.Length > 200
                ? messageEntity.Text.Substring(0, 200) + "..."
                : messageEntity.Text ?? "(empty)";

            logger.LogInformation(
                "User message details - MessageId: {MessageId}, GameId: {GameId}, PlayerId: {PlayerId}, " +
                "TextLength: {TextLength}, CreatedAt: {CreatedAt}, TextPreview: {TextPreview}",
                messageEntity.Id,
                messageEntity.GameId,
                messageEntity.PlayerId ?? "(none)",
                messageEntity.Text?.Length ?? 0,
                messageEntity.CreatedAt,
                textPreview);

            // Enqueue message for embedding
            ConversationMessageReadyForEmbeddingMessage embeddingMessage = new()
            {
                MessageId = messageEntity.Id,
                GameId = messageEntity.GameId,
                Text = messageEntity.Text ?? string.Empty,
                Role = ChatRole.User,
                CreatedAt = messageEntity.CreatedAt
            };
            await messagePublisher.PublishAsync(embeddingMessage, cancellationToken);
            logger.LogDebug("Enqueued user message {MessageId} for embedding", messageEntity.Id);

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process user message {MessageId}", message.MessageId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            // Re-throw to let message consumer service handle retry logic
            throw;
        }
    }
}

