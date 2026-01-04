using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.Workers.AssistantMessageWorker.Services;
using Microsoft.EntityFrameworkCore;

namespace MattEland.Jaimes.Workers.AssistantMessageWorker.Consumers;

/// <summary>
/// Consumer for individual evaluator tasks, enabling parallel evaluation across worker instances.
/// </summary>
public class EvaluatorTaskConsumer(
    IDbContextFactory<JaimesDbContext> contextFactory,
    IMessageEvaluationService evaluationService,
    IInstructionService instructionService,
    IMessageUpdateNotifier messageUpdateNotifier,
    ILogger<EvaluatorTaskConsumer> logger,
    ActivitySource activitySource) : IMessageConsumer<EvaluatorTaskMessage>
{
    public async Task HandleAsync(EvaluatorTaskMessage message, CancellationToken cancellationToken = default)
    {
        using Activity? activity = activitySource.StartActivity("EvaluatorTask.Process");
        activity?.SetTag("messaging.message_type", nameof(EvaluatorTaskMessage));
        activity?.SetTag("message.id", message.MessageId);
        activity?.SetTag("message.game_id", message.GameId.ToString());
        activity?.SetTag("evaluator.name", message.EvaluatorName);
        activity?.SetTag("evaluator.index", message.EvaluatorIndex);
        activity?.SetTag("evaluator.total", message.TotalEvaluators);
        activity?.SetTag("evaluator.batch_id", message.BatchId.ToString());

        logger.LogInformation(
            "Processing evaluator task: MessageId={MessageId}, Evaluator={EvaluatorName} ({Index}/{Total}), BatchId={BatchId}",
            message.MessageId,
            message.EvaluatorName,
            message.EvaluatorIndex,
            message.TotalEvaluators,
            message.BatchId);

        try
        {
            // Notify evaluator started
            await messageUpdateNotifier.NotifyEvaluatorStartedAsync(
                message.MessageId,
                message.GameId,
                message.EvaluatorName,
                message.EvaluatorIndex,
                message.TotalEvaluators,
                cancellationToken);

            // Load the message from database
            await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
            Message? messageEntity = await context.Messages
                .Include(m => m.Game)
                .Include(m => m.Agent)
                .Include(m => m.InstructionVersion)
                .FirstOrDefaultAsync(m => m.Id == message.MessageId, cancellationToken);

            if (messageEntity == null)
            {
                logger.LogWarning(
                    "Message {MessageId} not found in database for evaluator {EvaluatorName}",
                    message.MessageId,
                    message.EvaluatorName);
                
                // Write a completion marker so the evaluator is counted as complete
                await WriteEvaluatorCompletionMarkerAsync(
                    context,
                    message.MessageId,
                    message.EvaluatorName,
                    "Message not found in database",
                    cancellationToken);
                
                // Send completion notification to prevent UI from showing perpetual "started" state
                await messageUpdateNotifier.NotifyEvaluatorCompletedAsync(
                    message.MessageId,
                    message.GameId,
                    message.EvaluatorName,
                    message.EvaluatorIndex,
                    message.TotalEvaluators,
                    cancellationToken);
                
                activity?.SetStatus(ActivityStatusCode.Error, "Message not found");
                return;
            }

            // Get the system prompt for this message's game
            string? systemPrompt = await instructionService.GetInstructionsForGameAsync(
                messageEntity.GameId,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(systemPrompt))
            {
                logger.LogWarning(
                    "No system prompt found for game {GameId}, using default",
                    messageEntity.GameId);
                systemPrompt = "You are a helpful game master assistant.";
            }

            // Get conversation context (last 5 messages before this one, in chronological order)
            // Use CreatedAt for accurate chronological ordering, with ID as tiebreaker for concurrent inserts
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

            // Run only the specified evaluator
            await evaluationService.EvaluateSingleEvaluatorAsync(
                messageEntity,
                systemPrompt,
                conversationContext,
                message.EvaluatorName,
                cancellationToken);

            logger.LogInformation(
                "Evaluator {EvaluatorName} ({Index}/{Total}) completed for message {MessageId}",
                message.EvaluatorName,
                message.EvaluatorIndex,
                message.TotalEvaluators,
                message.MessageId);

            // Notify evaluator completed
            await messageUpdateNotifier.NotifyEvaluatorCompletedAsync(
                message.MessageId,
                message.GameId,
                message.EvaluatorName,
                message.EvaluatorIndex,
                message.TotalEvaluators,
                cancellationToken);

            // Check if all evaluators have completed by counting distinct evaluators in database
            // This handles parallel execution where evaluators can complete in any order
            int completedEvaluatorCount = await context.MessageEvaluationMetrics
                .Where(m => m.MessageId == message.MessageId && m.EvaluatorId != null)
                .Select(m => m.EvaluatorId)
                .Distinct()
                .CountAsync(cancellationToken);

            if (completedEvaluatorCount >= message.TotalEvaluators)
            {
                // All evaluators for this message have completed - notify stage completion
                await messageUpdateNotifier.NotifyStageCompletedAsync(
                    message.MessageId,
                    message.GameId,
                    MessagePipelineType.Assistant,
                    MessagePipelineStage.Evaluation,
                    cancellationToken);
                
                logger.LogInformation(
                    "All {TotalEvaluators} evaluators completed for message {MessageId} (found {CompletedCount} distinct evaluators), batch {BatchId}",
                    message.TotalEvaluators,
                    message.MessageId,
                    completedEvaluatorCount,
                    message.BatchId);
            }

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error running evaluator {EvaluatorName} for message {MessageId}",
                message.EvaluatorName,
                message.MessageId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw; // Let MQ retry
        }
    }

    /// <summary>
    /// Writes a completion marker for an evaluator that failed or was skipped.
    /// This ensures the evaluator is counted in completion tracking.
    /// </summary>
    private async Task WriteEvaluatorCompletionMarkerAsync(
        JaimesDbContext context,
        int messageId,
        string evaluatorName,
        string reason,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get or create the evaluator entity
            var evaluatorEntity = await context.Evaluators
                .FirstOrDefaultAsync(e => e.Name.ToLower() == evaluatorName.ToLower(), cancellationToken);

            if (evaluatorEntity == null)
            {
                // Create a new evaluator entity if it doesn't exist
                evaluatorEntity = new Evaluator
                {
                    Name = evaluatorName,
                    Description = $"Auto-created for {evaluatorName}",
                    CreatedAt = DateTime.UtcNow
                };
                context.Evaluators.Add(evaluatorEntity);
                await context.SaveChangesAsync(cancellationToken);
            }

            // Write a marker metric with score -1 to indicate failure/skip
            MessageEvaluationMetric marker = new()
            {
                MessageId = messageId,
                MetricName = $"{evaluatorName}_Completion",
                Score = -1, // Negative score indicates failure/skip
                Remarks = reason,
                EvaluatedAt = DateTime.UtcNow,
                EvaluatorId = evaluatorEntity.Id
            };

            context.MessageEvaluationMetrics.Add(marker);
            await context.SaveChangesAsync(cancellationToken);
            
            logger.LogInformation(
                "Wrote completion marker for evaluator {EvaluatorName} on message {MessageId}: {Reason}",
                evaluatorName,
                messageId,
                reason);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to write completion marker for evaluator {EvaluatorName} on message {MessageId}",
                evaluatorName,
                messageId);
            // Don't rethrow - this is a best-effort marker
        }
    }
}
