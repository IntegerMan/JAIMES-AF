using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.Workers.AssistantMessageWorker.Services;

namespace MattEland.Jaimes.Workers.AssistantMessageWorker.Consumers;

public class AssistantMessageConsumer(
    IDbContextFactory<JaimesDbContext> contextFactory,
    IMessagePublisher messagePublisher,
    ILogger<AssistantMessageConsumer> logger,
    ActivitySource activitySource,
    IMessageEvaluationService evaluationService,
    IInstructionService instructionService) : IMessageConsumer<ConversationMessageQueuedMessage>
{
    public async Task HandleAsync(ConversationMessageQueuedMessage message,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = activitySource.StartActivity("AssistantMessage.Process");
        activity?.SetTag("messaging.message_type", nameof(ConversationMessageQueuedMessage));
        activity?.SetTag("message.id", message.MessageId);
        activity?.SetTag("message.game_id", message.GameId.ToString());
        activity?.SetTag("message.role", message.Role.ToString());
        activity?.SetTag("message.evaluate_missing_only", message.EvaluateMissingOnly);

        try
        {
            // Note: Role-based routing ensures only Assistant messages reach this consumer
            // No need to filter by role here

            logger.LogInformation(
                "Processing assistant message: MessageId={MessageId}, GameId={GameId}, EvaluateMissingOnly={EvaluateMissingOnly}",
                message.MessageId,
                message.GameId,
                message.EvaluateMissingOnly);

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

            activity?.SetTag("message.instruction_version_id", messageEntity.InstructionVersionId);

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
                messageEntity.InstructionVersionId,
                messageEntity.Text?.Length ?? 0,
                messageEntity.CreatedAt,
                textPreview);

            // Evaluate the assistant message
            using Activity? evaluationActivity = activitySource.StartActivity("AssistantMessage.Evaluate");
            evaluationActivity?.SetTag("message.id", messageEntity.Id);
            evaluationActivity?.SetTag("message.game_id", messageEntity.GameId.ToString());

            try
            {
                // Load last 5 messages for conversation context (ordered by CreatedAt descending, take 5, reverse)
                List<Message> conversationContext = await context.Messages
                    .Where(m => m.GameId == messageEntity.GameId && m.CreatedAt <= messageEntity.CreatedAt &&
                                m.Id != messageEntity.Id)
                    .OrderByDescending(m => m.CreatedAt)
                    .ThenByDescending(m => m.Id)
                    .Take(5)
                    .OrderBy(m => m.CreatedAt)
                    .ThenBy(m => m.Id)
                    .ToListAsync(cancellationToken);

                logger.LogDebug(
                    "Loaded {Count} messages for evaluation context (game {GameId})",
                    conversationContext.Count,
                    messageEntity.GameId);

                // Get system prompt/instructions for the scenario
                string? systemPrompt = null;
                if (messageEntity.Game?.ScenarioId != null)
                {
                    systemPrompt = await instructionService.GetInstructionsAsync(
                        messageEntity.Game.ScenarioId,
                        cancellationToken);
                }

                if (string.IsNullOrWhiteSpace(systemPrompt))
                {
                    logger.LogWarning(
                        "No system prompt found for scenario {ScenarioId}, using default",
                        messageEntity.Game?.ScenarioId ?? "(unknown)");
                    systemPrompt = "You are a helpful game master assistant.";
                }

                evaluationActivity?.SetTag("evaluation.context_message_count", conversationContext.Count);
                evaluationActivity?.SetTag("evaluation.system_prompt_length", systemPrompt.Length);

                // Perform evaluation (optionally with filtered evaluators)
                await evaluationService.EvaluateMessageAsync(
                    messageEntity,
                    systemPrompt,
                    conversationContext,
                    message.EvaluatorsToRun,
                    cancellationToken);

                evaluationActivity?.SetStatus(ActivityStatusCode.Ok);
                logger.LogDebug("Successfully evaluated message {MessageId}", messageEntity.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to evaluate message {MessageId}", messageEntity.Id);
                evaluationActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                // Continue processing - don't fail the message if evaluation fails
            }

            // Enqueue message for embedding (skip if only evaluating missing evaluators)
            if (!message.EvaluateMissingOnly)
            {
                ConversationMessageReadyForEmbeddingMessage embeddingMessage = new()
                {
                    MessageId = messageEntity.Id,
                    GameId = messageEntity.GameId,
                    Text = messageEntity.Text ?? string.Empty,
                    Role = ChatRole.Assistant,
                    CreatedAt = messageEntity.CreatedAt
                };
                await messagePublisher.PublishAsync(embeddingMessage, cancellationToken);
                logger.LogDebug("Enqueued assistant message {MessageId} for embedding", messageEntity.Id);
            }
            else
            {
                logger.LogDebug("Skipping embedding for message {MessageId} (EvaluateMissingOnly=true)",
                    messageEntity.Id);
            }

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

