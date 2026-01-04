using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.Workers.AssistantMessageWorker.Services;

namespace MattEland.Jaimes.Workers.AssistantMessageWorker.Consumers;

public class AssistantMessageConsumer(
    IDbContextFactory<JaimesDbContext> contextFactory,
    IMessagePublisher messagePublisher,
    ILogger<AssistantMessageConsumer> logger,
    ActivitySource activitySource,
    IMessageEvaluationService evaluationService,
    IInstructionService instructionService,
    IMessageUpdateNotifier messageUpdateNotifier) : IMessageConsumer<ConversationMessageQueuedMessage>
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
                "Processing assistant message: MessageId={MessageId}, GameId={GameId}, EvaluateMissingOnly={EvaluateMissingOnly}, EvaluatorsToRun={EvaluatorsToRun}",
                message.MessageId,
                message.GameId,
                message.EvaluateMissingOnly,
                message.EvaluatorsToRun != null ? string.Join(", ", message.EvaluatorsToRun) : "(all)");

            // Notify pipeline stage: Loading
            await messageUpdateNotifier.NotifyStageStartedAsync(
                message.MessageId,
                message.GameId,
                MessagePipelineType.Assistant,
                MessagePipelineStage.Loading,
                cancellationToken: cancellationToken);

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

                await messageUpdateNotifier.NotifyStageFailedAsync(
                    message.MessageId,
                    message.GameId,
                    MessagePipelineType.Assistant,
                    MessagePipelineStage.Loading,
                    cancellationToken);
                return;
            }

            // Notify pipeline stage: Loading completed
            await messageUpdateNotifier.NotifyStageCompletedAsync(
                message.MessageId,
                message.GameId,
                MessagePipelineType.Assistant,
                MessagePipelineStage.Loading,
                cancellationToken);

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
                // Load last 5 messages for conversation context (ordered by CreatedAt, with ID as tiebreaker)
                // Use CreatedAt < (not <=) to exclude the current message, with ID tiebreaker for concurrent inserts
                List<Message> conversationContext = await context.Messages
                    .Where(m => m.GameId == messageEntity.GameId && 
                               (m.CreatedAt < messageEntity.CreatedAt || 
                                (m.CreatedAt == messageEntity.CreatedAt && m.Id < messageEntity.Id)))
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

                // Get the list of evaluators to run
                IReadOnlyList<string> availableEvaluators = evaluationService.GetAvailableEvaluatorNames();
                IEnumerable<string> evaluatorsToRun = message.EvaluatorsToRun ?? availableEvaluators;
                var evaluatorList = evaluatorsToRun.ToList();

                if (evaluatorList.Count > 0)
                {
                    // Notify pipeline stage: Evaluation
                    await messageUpdateNotifier.NotifyStageStartedAsync(
                        messageEntity.Id,
                        messageEntity.GameId,
                        MessagePipelineType.Assistant,
                        MessagePipelineStage.Evaluation,
                        messageEntity.Text,
                        cancellationToken);

                    // Publish individual evaluator tasks for parallel processing
                    Guid batchId = Guid.NewGuid();
                    int totalEvaluators = evaluatorList.Count;

                    for (int i = 0; i < totalEvaluators; i++)
                    {
                        EvaluatorTaskMessage evaluatorTask = new()
                        {
                            MessageId = messageEntity.Id,
                            GameId = messageEntity.GameId,
                            EvaluatorName = evaluatorList[i],
                            EvaluatorIndex = i + 1,
                            TotalEvaluators = totalEvaluators,
                            BatchId = batchId
                        };
                        await messagePublisher.PublishAsync(evaluatorTask, cancellationToken);
                    }

                    logger.LogInformation(
                        "Published {Count} evaluator tasks for message {MessageId} (BatchId: {BatchId})",
                        totalEvaluators,
                        messageEntity.Id,
                        batchId);

                    // Note: Stage completion will be notified by EvaluatorTaskConsumer after all evaluators finish
                }

                evaluationActivity?.SetStatus(ActivityStatusCode.Ok);
                logger.LogDebug("Successfully dispatched evaluators for message {MessageId}", messageEntity.Id);
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
                // Notify pipeline stage: Embedding Queue
                await messageUpdateNotifier.NotifyStageStartedAsync(
                    messageEntity.Id,
                    messageEntity.GameId,
                    MessagePipelineType.Assistant,
                    MessagePipelineStage.EmbeddingQueue,
                    messageEntity.Text,
                    cancellationToken);

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

                // Notify pipeline stage: Embedding Queue completed
                await messageUpdateNotifier.NotifyStageCompletedAsync(
                    messageEntity.Id,
                    messageEntity.GameId,
                    MessagePipelineType.Assistant,
                    MessagePipelineStage.EmbeddingQueue,
                    cancellationToken);
            }
            else
            {
                logger.LogDebug("Skipping embedding for message {MessageId} (EvaluateMissingOnly=true)",
                    messageEntity.Id);
            }

            // Notify pipeline stage: Complete
            await messageUpdateNotifier.NotifyStageCompletedAsync(
                messageEntity.Id,
                messageEntity.GameId,
                MessagePipelineType.Assistant,
                MessagePipelineStage.Complete,
                cancellationToken);

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process assistant message {MessageId}", message.MessageId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            // Notify pipeline stage: Failed
            await messageUpdateNotifier.NotifyStageFailedAsync(
                message.MessageId,
                message.GameId,
                MessagePipelineType.Assistant,
                MessagePipelineStage.Failed,
                cancellationToken);

            // Re-throw to let message consumer service handle retry logic
            throw;
        }
    }
}

