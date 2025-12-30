using System.Text.Json;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefaults;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;

namespace MattEland.Jaimes.Workers.AssistantMessageWorker.Services;

/// <summary>
/// Service for evaluating assistant messages using AI evaluation metrics.
/// </summary>
public class MessageEvaluationService(
    IDbContextFactory<JaimesDbContext> contextFactory,
    RelevanceTruthAndCompletenessEvaluator evaluator,
    IChatClient chatClient,
    TextGenerationModelOptions modelOptions,
    ILogger<MessageEvaluationService> logger) : IMessageEvaluationService
{
    public async Task EvaluateMessageAsync(
        Message message,
        string systemPrompt,
        List<Message> conversationContext,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message.Text))
        {
            logger.LogWarning("Cannot evaluate message {MessageId} - message text is empty", message.Id);
            return;
        }

        try
        {
            // Build the conversation history for evaluation context
            // Take last 5 messages from conversation context (already filtered)
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

            // The assistant message to evaluate - create a ChatResponse
            ChatMessage assistantChatMessage = new(ChatRole.Assistant, message.Text);
            ChatResponse assistantResponse = new(assistantChatMessage);

            logger.LogDebug(
                "Evaluating message {MessageId} with context of {ContextCount} messages",
                message.Id,
                evaluationContext.Count);

            // Create ChatConfiguration for the evaluator
            ChatConfiguration chatConfiguration = new(chatClient);

            // Perform evaluation
            EvaluationResult result = await evaluator.EvaluateAsync(
                evaluationContext,
                assistantResponse,
                chatConfiguration);

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

            // Extract and store each metric using the metric names from the evaluator
            string[] metricNames = new[]
            {
                RelevanceTruthAndCompletenessEvaluator.RelevanceMetricName,
                RelevanceTruthAndCompletenessEvaluator.TruthMetricName,
                RelevanceTruthAndCompletenessEvaluator.CompletenessMetricName
            };

            foreach (string metricName in metricNames)
            {
                NumericMetric? metric = result.Get<NumericMetric>(metricName);
                if (metric != null && metric.Value.HasValue)
                {
                    // Serialize any additional metadata if available
                    string? diagnosticsJson = null;
                    try
                    {
                        // Try to get any additional context from the result
                        var diagnostics = new Dictionary<string, object?>
                        {
                            {"MetricName", metricName},
                            {"EvaluatedAt", evaluatedAt}
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
                        Remarks = null, // NumericMetric doesn't expose reasoning/remarks directly
                        EvaluatedAt = evaluatedAt,
                        Diagnostics = diagnosticsJson,
                        EvaluationModelId = evaluationModel?.Id
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

            logger.LogInformation(
                "Successfully evaluated message {MessageId} with metrics",
                message.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to evaluate message {MessageId}", message.Id);
            throw;
        }
    }
}
