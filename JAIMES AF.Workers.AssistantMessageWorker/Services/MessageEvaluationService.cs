using System.Linq;
using System.Text.Json;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefaults;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Reporting;
using MattEland.Jaimes.Evaluators;

namespace MattEland.Jaimes.Workers.AssistantMessageWorker.Services;

/// <summary>
/// Service for evaluating assistant messages using AI evaluation metrics.
/// Runs evaluators in parallel and streams results as they complete.
/// </summary>
public class MessageEvaluationService(
    IDbContextFactory<JaimesDbContext> contextFactory,
    IEnumerable<IEvaluator> evaluators,
    IChatClient chatClient,
    TextGenerationModelOptions modelOptions,
    IEvaluationResultStore resultStore,
    ILogger<MessageEvaluationService> logger,
    IMessageUpdateNotifier messageUpdateNotifier) : IMessageEvaluationService
{
    public async Task EvaluateMessageAsync(
        Message message,
        string systemPrompt,
        List<Message> conversationContext,
        IEnumerable<string>? evaluatorsToRun = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message.Text))
        {
            logger.LogWarning("Cannot evaluate message {MessageId} - message text is empty", message.Id);
            return;
        }

        // Log available evaluators for debugging
        var evaluatorList = evaluators.ToList();
        logger.LogInformation(
            "Available evaluators for message {MessageId}: {Count} evaluators ({EvaluatorNames})",
            message.Id,
            evaluatorList.Count,
            string.Join(", ", evaluatorList.Select(e => e.GetType().Name)));

        // Filter evaluators if specific ones are requested
        List<IEvaluator> activeEvaluators = evaluatorList;
        if (evaluatorsToRun != null && evaluatorsToRun.Any())
        {
            var evaluatorNamesSet = new HashSet<string>(evaluatorsToRun, StringComparer.OrdinalIgnoreCase);
            activeEvaluators = evaluatorList.Where(e => evaluatorNamesSet.Contains(e.GetType().Name)).ToList();

            logger.LogInformation(
                "Filtering evaluators for message {MessageId}: running {Count} of {Total} ({EvaluatorNames})",
                message.Id,
                activeEvaluators.Count,
                evaluatorList.Count,
                string.Join(", ", activeEvaluators.Select(e => e.GetType().Name)));

            if (activeEvaluators.Count == 0)
            {
                logger.LogWarning(
                    "No matching evaluators found for message {MessageId}. Requested: {RequestedEvaluators}",
                    message.Id, string.Join(", ", evaluatorsToRun));
                return;
            }
        }

        // Build the conversation history for evaluation context
        List<ChatMessage> chatMessages = conversationContext
            .TakeLast(5)
            .Select(m => new ChatMessage(
                m.PlayerId == null ? ChatRole.Assistant : ChatRole.User,
                m.Text ?? string.Empty))
            .ToList();

        // Add system prompt as the first message
        List<ChatMessage> evaluationContext =
        [
            new ChatMessage(ChatRole.System, systemPrompt)
        ];
        evaluationContext.AddRange(chatMessages);

        // The assistant message to evaluate
        ChatMessage assistantChatMessage = new(ChatRole.Assistant, message.Text);
        ChatResponse assistantResponse = new(assistantChatMessage);

        logger.LogDebug(
            "Evaluating message {MessageId} with context of {ContextCount} messages using {EvaluatorCount} evaluators in parallel",
            message.Id,
            evaluationContext.Count,
            activeEvaluators.Count);

        // Calculate total expected metrics (RTC = 3, others = 1)
        int totalExpectedMetrics = EvaluatorMetricCountHelper.CalculateTotalExpectedMetrics(activeEvaluators);
        int completedMetricCount = 0;

        // Get model info once for all evaluators
        await using JaimesDbContext modelContext = await contextFactory.CreateDbContextAsync(cancellationToken);
        Model? evaluationModel = await modelContext.GetOrCreateModelAsync(
            modelOptions.Name,
            modelOptions.Provider.ToString(),
            modelOptions.Endpoint,
            logger,
            cancellationToken);

        // Run evaluators individually for progress tracking
        var activeEvaluatorList = activeEvaluators.ToList();
        int totalEvaluators = activeEvaluatorList.Count;
        var allResults = new EvaluationResult();

        // Load evaluator ID lookup once
        var evaluatorIdLookup = await modelContext.Evaluators
            .ToDictionaryAsync(e => e.Name.ToLower(), e => e.Id, cancellationToken);

        // Create ChatConfiguration for evaluators
        ChatConfiguration chatConfiguration = new(chatClient);

        // Track all metrics for final notification
        List<MessageEvaluationMetricResponse> allMetricResponses = [];

        for (int i = 0; i < totalEvaluators; i++)
        {
            var evaluator = activeEvaluatorList[i];
            string evaluatorName = evaluator.GetType().Name;
            int evaluatorIndex = i + 1;

            // Notify evaluator started
            await messageUpdateNotifier.NotifyEvaluatorStartedAsync(
                message.Id,
                message.GameId,
                evaluatorName,
                evaluatorIndex,
                totalEvaluators,
                cancellationToken);

            try
            {
                // Create ReportingConfiguration for this single evaluator
                ReportingConfiguration reportConfig = new(
                    [evaluator],
                    resultStore,
                    executionName: $"Assistant Message Quality - {evaluatorName}",
                    chatConfiguration: chatConfiguration);

                // Perform evaluation for this evaluator
                EvaluationResult result = await PerformEvaluationInternalAsync(
                    reportConfig,
                    message,
                    evaluationContext,
                    assistantResponse,
                    cancellationToken);

                // Merge results and save to database
                DateTime evaluatedAt = DateTime.UtcNow;
                foreach (var kvp in result.Metrics)
                {
                    allResults.Metrics[kvp.Key] = kvp.Value;

                    // Save each metric to database
                    string metricName = kvp.Key;
                    if (kvp.Value is NumericMetric metric && metric.Value != null)
                    {
                        // Find evaluator ID
                        int? evaluatorId = null;
                        if (evaluatorIdLookup.TryGetValue(evaluatorName.ToLower(), out int id))
                        {
                            evaluatorId = id;
                        }

                        // Serialize diagnostics
                        string? diagnosticsJson = null;
                        try
                        {
                            var diagnostics = new Dictionary<string, object?>
                            {
                                { "MetricName", metricName },
                                { "EvaluatedAt", evaluatedAt },
                                { "Messages", metric.Diagnostics?.Select(d => d.Message).ToList() ?? [] }
                            };
                            diagnosticsJson = JsonSerializer.Serialize(diagnostics);
                        }
                        catch (Exception diagEx)
                        {
                            logger.LogWarning(diagEx, "Failed to serialize diagnostics for metric {MetricName}",
                                metricName);
                        }

                        // Save metric to database
                        await using JaimesDbContext metricContext =
                            await contextFactory.CreateDbContextAsync(cancellationToken);
                        MessageEvaluationMetric evaluationMetric = new()
                        {
                            MessageId = message.Id,
                            MetricName = metricName,
                            Score = metric.Value.Value,
                            Remarks = metric.Reason,
                            EvaluatedAt = evaluatedAt,
                            Diagnostics = diagnosticsJson,
                            EvaluationModelId = evaluationModel?.Id,
                            EvaluatorId = evaluatorId
                        };

                        metricContext.MessageEvaluationMetrics.Add(evaluationMetric);
                        await metricContext.SaveChangesAsync(cancellationToken);

                        logger.LogDebug(
                            "Stored evaluation metric: MessageId={MessageId}, MetricName={MetricName}, Score={Score}, EvaluatorId={EvaluatorId}",
                            message.Id,
                            metricName,
                            metric.Value.Value,
                            evaluatorId);

                        // Create response for notification
                        var metricResponse = new MessageEvaluationMetricResponse
                        {
                            Id = evaluationMetric.Id,
                            MessageId = message.Id,
                            MetricName = metricName,
                            Score = metric.Value.Value,
                            Remarks = metric.Reason,
                            EvaluatedAt = evaluatedAt,
                            EvaluatorId = evaluatorId,
                            EvaluatorName = evaluatorName
                        };

                        allMetricResponses.Add(metricResponse);

                        // Increment completed count and notify
                        completedMetricCount++;

                        // Send streaming notification for this metric
                        await messageUpdateNotifier.NotifyMetricEvaluatedAsync(
                            message.Id,
                            message.GameId,
                            metricResponse,
                            totalExpectedMetrics,
                            completedMetricCount,
                            isError: false,
                            errorMessage: null,
                            cancellationToken);
                    }
                }

                logger.LogDebug(
                    "Evaluator {EvaluatorName} ({Index}/{Total}) completed for message {MessageId}",
                    evaluatorName,
                    evaluatorIndex,
                    totalEvaluators,
                    message.Id);

                // Notify evaluator completed (only on success)
                await messageUpdateNotifier.NotifyEvaluatorCompletedAsync(
                    message.Id,
                    message.GameId,
                    evaluatorName,
                    evaluatorIndex,
                    totalEvaluators,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Evaluator {EvaluatorName} failed for message {MessageId}", evaluatorName,
                    message.Id);

                // Create error metric responses - one for each expected metric from this evaluator
                int expectedForThisEvaluator = EvaluatorMetricCountHelper.GetExpectedMetricCount(evaluator);
                DateTime errorTime = DateTime.UtcNow;

                for (int j = 0; j < expectedForThisEvaluator; j++)
                {
                    string metricName = expectedForThisEvaluator > 1
                        ? $"{evaluatorName}_{j + 1}"
                        : evaluatorName;

                    var errorMetricResponse = new MessageEvaluationMetricResponse
                    {
                        MessageId = message.Id,
                        MetricName = metricName,
                        Score = 0,
                        Remarks = $"Evaluation failed: {ex.Message}",
                        EvaluatedAt = errorTime,
                        EvaluatorName = evaluatorName
                    };

                    allMetricResponses.Add(errorMetricResponse);

                    // Increment completed count (even for errors, we're "done" with this metric)
                    completedMetricCount++;

                    // Send error notification
                    await messageUpdateNotifier.NotifyMetricEvaluatedAsync(
                        message.Id,
                        message.GameId,
                        errorMetricResponse,
                        totalExpectedMetrics,
                        completedMetricCount,
                        isError: true,
                        errorMessage: ex.Message,
                        cancellationToken);
                }
            }
        }

        // Calculate if all evaluators have run
        await using JaimesDbContext finalContext = await contextFactory.CreateDbContextAsync(cancellationToken);
        var totalEvaluatorCount = await finalContext.Evaluators.CountAsync(cancellationToken);
        var msgEvaluatorCount = await finalContext.MessageEvaluationMetrics
            .Where(m => m.MessageId == message.Id && m.EvaluatorId.HasValue)
            .Select(m => m.EvaluatorId)
            .Distinct()
            .CountAsync(cancellationToken);
        bool hasMissingEvaluators = msgEvaluatorCount < totalEvaluatorCount;

        // Send final MetricsEvaluated notification with all results
        if (allMetricResponses.Count > 0)
        {
            await messageUpdateNotifier.NotifyMetricsEvaluatedAsync(
                message.Id,
                message.GameId,
                allMetricResponses,
                message.Text,
                hasMissingEvaluators,
                cancellationToken);
        }

        logger.LogInformation(
            "Successfully evaluated message {MessageId} with {MetricCount} metrics from {EvaluatorCount} evaluators",
            message.Id,
            allMetricResponses.Count,
            activeEvaluators.Count);
    }

    private static async Task<EvaluationResult> PerformEvaluationInternalAsync(
        ReportingConfiguration reportConfig,
        Message message,
        List<ChatMessage> evaluationContext,
        ChatResponse assistantResponse,
        CancellationToken cancellationToken)
    {
        await using ScenarioRun scenarioRun = await reportConfig.CreateScenarioRunAsync(
            scenarioName: $"Scenario {message.GameId}",
            iterationName: $"Message {message.Id}",
            cancellationToken: cancellationToken);

        return await scenarioRun.EvaluateAsync(
            evaluationContext,
            assistantResponse,
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetAvailableEvaluatorNames()
    {
        return evaluators.Select(e => e.GetType().Name).ToList();
    }

    /// <inheritdoc />
    public async Task EvaluateSingleEvaluatorAsync(
        Message message,
        string systemPrompt,
        List<Message> conversationContext,
        string evaluatorName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message.Text))
        {
            logger.LogWarning("Cannot evaluate message {MessageId} - message text is empty", message.Id);

            // Write a completion marker so the evaluator is counted as complete
            await WriteEvaluatorCompletionMarkerAsync(message.Id, evaluatorName, "Empty message text",
                cancellationToken);
            return;
        }

        // Find the specific evaluator
        var evaluator = evaluators.FirstOrDefault(e =>
            e.GetType().Name.Equals(evaluatorName, StringComparison.OrdinalIgnoreCase));

        if (evaluator == null)
        {
            logger.LogWarning(
                "Evaluator {EvaluatorName} not found for message {MessageId}",
                evaluatorName,
                message.Id);

            // Write a completion marker so the evaluator is counted as complete
            await WriteEvaluatorCompletionMarkerAsync(message.Id, evaluatorName, "Evaluator not found",
                cancellationToken);
            return;
        }

        logger.LogInformation(
            "Running single evaluator {EvaluatorName} for message {MessageId}",
            evaluatorName,
            message.Id);

        try
        {
            // Build the conversation history for evaluation context
            List<ChatMessage> chatMessages = conversationContext
                .TakeLast(5)
                .Select(m => new ChatMessage(
                    m.PlayerId == null ? ChatRole.Assistant : ChatRole.User,
                    m.Text ?? string.Empty))
                .ToList();

            // Add system prompt as the first message
            List<ChatMessage> evaluationContext = new()
            {
                new ChatMessage(ChatRole.System, systemPrompt)
            };
            evaluationContext.AddRange(chatMessages);

            // The assistant message to evaluate
            ChatMessage assistantChatMessage = new(ChatRole.Assistant, message.Text);
            ChatResponse assistantResponse = new(assistantChatMessage);

            // Create ChatConfiguration for the evaluator
            ChatConfiguration chatConfiguration = new(chatClient);

            // Create ReportingConfiguration for this single evaluator
            ReportingConfiguration reportConfig = new(
                [evaluator],
                resultStore,
                executionName: $"Assistant Message Quality - {evaluatorName}",
                chatConfiguration: chatConfiguration);

            // Perform evaluation
            EvaluationResult result = await PerformEvaluationInternalAsync(
                reportConfig,
                message,
                evaluationContext,
                assistantResponse,
                cancellationToken);

            // Store metrics in database
            await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

            // Get or create the model for evaluation
            Model? evaluationModel = await context.GetOrCreateModelAsync(
                modelOptions.Name,
                modelOptions.Provider.ToString(),
                modelOptions.Endpoint,
                logger,
                cancellationToken);

            DateTime evaluatedAt = DateTime.UtcNow;

            // Get evaluator ID
            var evaluatorEntity = await context.Evaluators
                .FirstOrDefaultAsync(e => e.Name.ToLower() == evaluatorName.ToLower(), cancellationToken);

            foreach (var metricPair in result.Metrics)
            {
                string metricName = metricPair.Key;
                if (metricPair.Value is NumericMetric metric && metric.Value != null)
                {
                    // Serialize diagnostics
                    string? diagnosticsJson = null;
                    try
                    {
                        var diagnostics = new Dictionary<string, object?>
                        {
                            { "MetricName", metricName },
                            { "EvaluatedAt", evaluatedAt },
                            { "Messages", metric.Diagnostics?.Select(d => d.Message).ToList() ?? [] }
                        };
                        diagnosticsJson = JsonSerializer.Serialize(diagnostics);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to serialize diagnostics for metric {MetricName}", metricName);
                    }

                    MessageEvaluationMetric evaluationMetric = new()
                    {
                        MessageId = message.Id,
                        MetricName = metricName,
                        Score = metric.Value.Value,
                        Remarks = metric.Reason,
                        EvaluatedAt = evaluatedAt,
                        Diagnostics = diagnosticsJson,
                        EvaluationModelId = evaluationModel?.Id,
                        EvaluatorId = evaluatorEntity?.Id
                    };

                    context.MessageEvaluationMetrics.Add(evaluationMetric);

                    logger.LogDebug(
                        "Stored evaluation metric: MessageId={MessageId}, MetricName={MetricName}, Score={Score}",
                        message.Id,
                        metricName,
                        metric.Value.Value);
                }
            }

            await context.SaveChangesAsync(cancellationToken);

            // Build notification responses directly from saved entities (avoids DateTime precision issues with database)
            List<MessageEvaluationMetricResponse> metricResponses = [];
            foreach (var metricPair in result.Metrics)
            {
                string metricName = metricPair.Key;
                if (metricPair.Value is NumericMetric metric && metric.Value != null)
                {
                    // Find the saved entity to get its database-assigned ID
                    var savedMetric = context.MessageEvaluationMetrics.Local
                        .FirstOrDefault(m => m.MessageId == message.Id && m.MetricName == metricName);

                    metricResponses.Add(new MessageEvaluationMetricResponse
                    {
                        Id = savedMetric?.Id ?? 0,
                        MessageId = message.Id,
                        MetricName = metricName,
                        Score = metric.Value.Value,
                        Remarks = metric.Reason,
                        EvaluatedAt = evaluatedAt,
                        EvaluatorId = evaluatorEntity?.Id,
                        EvaluatorName = evaluatorName
                    });
                }
            }

            // Calculate if all evaluators have run
            var totalEvaluatorCount = await context.Evaluators.CountAsync(cancellationToken);
            var msgEvaluatorCount = await context.MessageEvaluationMetrics
                .Where(m => m.MessageId == message.Id && m.EvaluatorId.HasValue)
                .Select(m => m.EvaluatorId)
                .Distinct()
                .CountAsync(cancellationToken);
            bool hasMissingEvaluators = msgEvaluatorCount < totalEvaluatorCount;

            if (metricResponses.Count > 0)
            {
                await messageUpdateNotifier.NotifyMetricsEvaluatedAsync(
                    message.Id,
                    message.GameId,
                    metricResponses,
                    message.Text,
                    hasMissingEvaluators,
                    cancellationToken);
            }

            logger.LogInformation(
                "Successfully ran evaluator {EvaluatorName} for message {MessageId}",
                evaluatorName,
                message.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to run evaluator {EvaluatorName} for message {MessageId}",
                evaluatorName, message.Id);

            // Write a completion marker so the evaluator is counted as complete even on failure
            await WriteEvaluatorCompletionMarkerAsync(message.Id, evaluatorName, $"Exception: {ex.Message}",
                cancellationToken);

            throw;
        }
    }

    /// <summary>
    /// Writes a completion marker for an evaluator that failed or was skipped.
    /// This ensures the evaluator is counted in completion tracking.
    /// </summary>
    private async Task WriteEvaluatorCompletionMarkerAsync(
        int messageId,
        string evaluatorName,
        string reason,
        CancellationToken cancellationToken)
    {
        try
        {
            await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

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
