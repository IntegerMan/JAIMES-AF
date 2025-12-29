namespace MattEland.Jaimes.Workers.AssistantMessageWorker.Consumers;

public class AssistantMessageConsumer(
    IDbContextFactory<JaimesDbContext> contextFactory,
    ILogger<AssistantMessageConsumer> logger,
    ActivitySource activitySource) : IMessageConsumer<ConversationMessageQueuedMessage>
{
    public async Task HandleAsync(ConversationMessageQueuedMessage message, CancellationToken cancellationToken = default)
    {
        using Activity? activity = activitySource.StartActivity("AssistantMessage.Process");
        activity?.SetTag("messaging.message_type", nameof(ConversationMessageQueuedMessage));
        activity?.SetTag("message.id", message.MessageId);
        activity?.SetTag("message.game_id", message.GameId.ToString());
        activity?.SetTag("message.role", message.Role.ToString());

        try
        {
            // Note: Role-based routing ensures only Assistant messages reach this consumer
            // No need to filter by role here
            
            logger.LogInformation(
                "Processing assistant message: MessageId={MessageId}, GameId={GameId}",
                message.MessageId,
                message.GameId);

            // Load message from database
            await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
            Message? messageEntity = await context.Messages
                .Include(m => m.Game)
                .Include(m => m.Agent)
                .Include(m => m.InstructionVersion)
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
            if (messageEntity.AgentId != null)
            {
                activity?.SetTag("message.agent_id", messageEntity.AgentId);
            }
            if (messageEntity.InstructionVersionId != null)
            {
                activity?.SetTag("message.instruction_version_id", messageEntity.InstructionVersionId);
            }

            // Log message details
            string textPreview = messageEntity.Text?.Length > 200
                ? messageEntity.Text.Substring(0, 200) + "..."
                : messageEntity.Text ?? "(empty)";

            logger.LogInformation(
                "Assistant message details - MessageId: {MessageId}, GameId: {GameId}, AgentId: {AgentId}, " +
                "InstructionVersionId: {InstructionVersionId}, TextLength: {TextLength}, CreatedAt: {CreatedAt}, " +
                "TextPreview: {TextPreview}",
                messageEntity.Id,
                messageEntity.GameId,
                messageEntity.AgentId ?? "(none)",
                messageEntity.InstructionVersionId?.ToString() ?? "(none)",
                messageEntity.Text?.Length ?? 0,
                messageEntity.CreatedAt,
                textPreview);

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process assistant message {MessageId}", message.MessageId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            // Re-throw to let message consumer service handle retry logic
            throw;
        }
    }
}

