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

            // Get conversation context
            List<Message> conversationContext = await context.Messages
                .Where(m => m.GameId == messageEntity.GameId && m.Id < messageEntity.Id)
                .OrderBy(m => m.CreatedAt)
                .Take(20)
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
}
