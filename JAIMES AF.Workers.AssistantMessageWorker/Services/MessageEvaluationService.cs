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
        Lock countLock = new();

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

        for (int i = 0; i < totalEvaluators; i++)
        {
            var evaluator = activeEvaluatorList[i];
            string evaluatorName = evaluator.GetType().Name;
            int evaluatorIndex = i + 1;

        // Create ChatConfiguration for evaluators
        ChatConfiguration chatConfiguration = new(chatClient);

        // Notify evaluator started
        await messageUpdateNotifier.NotifyEvaluatorStartedAsync(
            message.Id,
            message.GameId,
            evaluatorName,
            evaluatorIndex,
            totalEvaluators,
            cancellationToken);

        // Track all metrics for final notification
        List<MessageEvaluationMetricResponse> allMetricResponses = [];
        Lock metricsLock = new();
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

                    // Merge results
                    foreach (var kvp in result.Metrics)
                    {
                        allResults.Metrics[kvp.Key] = kvp.Value;
                    }

                    logger.LogDebug(
                        "Evaluator {EvaluatorName} ({Index}/{Total}) completed for message {MessageId}",
                        evaluatorName,
                        evaluatorIndex,
                        totalEvaluators,
                        message.Id);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Evaluator {EvaluatorName} failed for message {MessageId}", evaluatorName, message.Id);
                    // Continue with other evaluators
                }

                // Notify evaluator completed
                await messageUpdateNotifier.NotifyEvaluatorCompletedAsync(
                    message.Id,
                    message.GameId,
                    evaluatorName,
                    evaluatorIndex,
                    totalEvaluators,
                    cancellationToken);
            }

            // Store metrics in database
            await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Run each evaluator in parallel
        var evaluatorTasks = activeEvaluators.Select(async evaluator =>
        {
            string evaluatorName = evaluator.GetType().Name;
            try
            {
                logger.LogDebug("Starting evaluator {EvaluatorName} for message {MessageId}",
                    evaluatorName, message.Id);

                // Create ReportingConfiguration for this single evaluator
                ReportingConfiguration reportConfig = new(
                    [evaluator],
                    resultStore,
                    executionName: $"Evaluator: {evaluatorName}",
                    chatConfiguration: chatConfiguration);

                // Perform evaluation
                EvaluationResult result = await PerformEvaluationInternalAsync(
                    reportConfig,
                    message,
                    evaluationContext,
                    assistantResponse,
                    cancellationToken);

                DateTime evaluatedAt = DateTime.UtcNow;

                // Process each metric from this evaluator
                foreach (var metricPair in allResults.Metrics)
                {
                    string metricName = metricPair.Key;
                    if (metricPair.Value is NumericMetric metric && metric.Value != null)
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
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Failed to serialize diagnostics for metric {MetricName}", metricName);
                        }

                        // Save metric to database immediately
                        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
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

                        context.MessageEvaluationMetrics.Add(evaluationMetric);
                        await context.SaveChangesAsync(cancellationToken);

                        logger.LogDebug(
                            "Stored evaluation metric: MessageId={MessageId}, MetricName={MetricName}, Score={Score}, EvaluatorId={EvaluatorId}",
                            message.Id,
                            metricName,
                            metric.Value.Value,
                            evaluatorId);

                        // Create response for notification
                        var metricResponse = new MessageEvaluationMetricResponse
                        {
                            MessageId = message.Id,
                            MetricName = metricName,
                            Score = metric.Value.Value,
                            Remarks = metric.Reason,
                            EvaluatedAt = evaluatedAt,
                            EvaluatorId = evaluatorId,
                            EvaluatorName = evaluatorName
                        };

                        // Track for final notification
                        lock (metricsLock)
                        {
                            allMetricResponses.Add(metricResponse);
                        }

                        // Increment completed count and notify
                        int currentCompleted;
                        lock (countLock)
                        {
                            completedMetricCount++;
                            currentCompleted = completedMetricCount;
                        }

                        // Send streaming notification for this metric
                        await messageUpdateNotifier.NotifyMetricEvaluatedAsync(
                            message.Id,
                            message.GameId,
                            metricResponse,
                            totalExpectedMetrics,
                            currentCompleted,
                            isError: false,
                            errorMessage: null,
                            cancellationToken);
                    }
                }

                logger.LogDebug("Completed evaluator {EvaluatorName} for message {MessageId}",
                    evaluatorName, message.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Evaluator {EvaluatorName} failed for message {MessageId}",
                    evaluatorName, message.Id);

                // Create error metric responses - one for each expected metric from this evaluator
                // This ensures the count matches what completedMetricCount increments by
                int expectedForThisEvaluator = EvaluatorMetricCountHelper.GetExpectedMetricCount(evaluator);
                DateTime errorTime = DateTime.UtcNow;
                
                // Track all error metrics for final notification
                var errorMetrics = new List<MessageEvaluationMetricResponse>();
                for (int i = 0; i < expectedForThisEvaluator; i++)
                {
                    string metricName = expectedForThisEvaluator > 1 
                        ? $"{evaluatorName}_{i + 1}" 
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
                    errorMetrics.Add(errorMetricResponse);
                }

                lock (metricsLock)
                {
                    allMetricResponses.AddRange(errorMetrics);
                }

                // Increment completed count (even for errors, we're "done" with this evaluator)
                int currentCompleted;
                lock (countLock)
                {
                    completedMetricCount += expectedForThisEvaluator;
                    currentCompleted = completedMetricCount;
                }

                // Send error notifications for all error metrics so they all get proper error styling
                foreach (var errorMetric in errorMetrics)
                {
                    await messageUpdateNotifier.NotifyMetricEvaluatedAsync(
                        message.Id,
                        message.GameId,
                        errorMetric,
                        totalExpectedMetrics,
                        currentCompleted,
                        isError: true,
                        errorMessage: ex.Message,
                        cancellationToken);
                }
            }
        });

        // Wait for all evaluators to complete
        await Task.WhenAll(evaluatorTasks);

        // Calculate if all evaluators have run
        await using JaimesDbContext finalContext = await contextFactory.CreateDbContextAsync(cancellationToken);
        var totalEvaluatorCount = await finalContext.Evaluators.CountAsync(cancellationToken);
        var msgEvaluatorCount = await finalContext.MessageEvaluationMetrics
            .Where(m => m.MessageId == message.Id && m.EvaluatorId.HasValue)
            .Select(m => m.EvaluatorId)
            .Distinct()
            .CountAsync(cancellationToken);
        bool hasMissingEvaluators = msgEvaluatorCount < totalEvaluatorCount;

        // Notify web clients via SignalR
        List<MessageEvaluationMetricResponse> metricResponses = allResults.Metrics.Keys
            .Select(metricName =>
            {
                NumericMetric? metric = allResults.Get<NumericMetric>(metricName);
                return metric != null && metric.Value.HasValue
                    ? new MessageEvaluationMetricResponse
                    {
                        MessageId = message.Id,
                        MetricName = metricName,
                        Score = metric.Value.Value,
                        Remarks = metric.Reason,
                        EvaluatedAt = evaluatedAt
                    }
                    : null;
            })
            .Where(m => m != null)
            .Cast<MessageEvaluationMetricResponse>()
            .ToList();

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
}
