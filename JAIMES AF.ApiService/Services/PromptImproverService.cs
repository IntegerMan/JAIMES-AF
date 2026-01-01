using System.Linq;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.Text;

namespace MattEland.Jaimes.ApiService.Services;

/// <summary>
/// Service for generating AI-powered insights and improved prompts.
/// </summary>
public class PromptImproverService(
    IDbContextFactory<JaimesDbContext> contextFactory,
    IChatClient chatClient,
    ILogger<PromptImproverService> logger)
{
    private const int MaxItemsToAnalyze = 50;

    /// <summary>
    /// Generates insights based on the specified insight type.
    /// </summary>
    public async Task<GenerateInsightsResponse> GenerateInsightsAsync(
        string agentId,
        int versionId,
        string insightType,
        CancellationToken cancellationToken = default)
    {
        return insightType.ToLowerInvariant() switch
        {
            "feedback" => await GenerateFeedbackInsightsAsync(agentId, versionId, cancellationToken),
            "metrics" => await GenerateMetricsInsightsAsync(agentId, versionId, cancellationToken),
            "sentiment" => await GenerateSentimentInsightsAsync(agentId, versionId, cancellationToken),
            _ => new GenerateInsightsResponse
            {
                Success = false,
                Error = $"Unknown insight type: {insightType}",
                InsightType = insightType
            }
        };
    }

    /// <summary>
    /// Generates insights from feedback data.
    /// </summary>
    public async Task<GenerateInsightsResponse> GenerateFeedbackInsightsAsync(
        string agentId,
        int versionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

            // Get feedback for this version
            List<MessageFeedback> feedbackItems = await context.MessageFeedbacks
                .Where(f => f.InstructionVersionId == versionId)
                .OrderByDescending(f => f.CreatedAt)
                .Take(MaxItemsToAnalyze)
                .ToListAsync(cancellationToken);

            if (feedbackItems.Count == 0)
            {
                return new GenerateInsightsResponse
                {
                    Success = true,
                    InsightType = "feedback",
                    ItemsAnalyzed = 0,
                    Insights = "No feedback data available for analysis."
                };
            }

            // Get the current prompt for context
            string currentPrompt = await GetCurrentPromptAsync(context, versionId, cancellationToken);

            // Build the user message with feedback data
            string userMessage = BuildFeedbackInsightsPrompt(feedbackItems, currentPrompt);

            // Call the AI
            string insights = await GetAiResponseAsync(
                PromptImproverSystemPrompts.FeedbackInsightsPrompt,
                userMessage,
                cancellationToken);

            return new GenerateInsightsResponse
            {
                Success = true,
                InsightType = "feedback",
                ItemsAnalyzed = feedbackItems.Count,
                Insights = insights
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate feedback insights for agent {AgentId} version {VersionId}",
                agentId, versionId);
            return new GenerateInsightsResponse
            {
                Success = false,
                InsightType = "feedback",
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Generates insights from metrics data.
    /// </summary>
    public async Task<GenerateInsightsResponse> GenerateMetricsInsightsAsync(
        string agentId,
        int versionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

            // Get metrics for messages from this version
            List<MessageEvaluationMetric> metrics = await context.MessageEvaluationMetrics
                .Include(m => m.Message)
                .Where(m => m.Message != null && m.Message.InstructionVersionId == versionId)
                .OrderByDescending(m => m.EvaluatedAt)
                .Take(MaxItemsToAnalyze)
                .ToListAsync(cancellationToken);

            if (metrics.Count == 0)
            {
                return new GenerateInsightsResponse
                {
                    Success = true,
                    InsightType = "metrics",
                    ItemsAnalyzed = 0,
                    Insights = "No metrics data available for analysis."
                };
            }

            // Get the current prompt for context
            string currentPrompt = await GetCurrentPromptAsync(context, versionId, cancellationToken);

            // Build the user message with metrics data
            string userMessage = BuildMetricsInsightsPrompt(metrics, currentPrompt);

            // Call the AI
            string insights = await GetAiResponseAsync(
                PromptImproverSystemPrompts.MetricsInsightsPrompt,
                userMessage,
                cancellationToken);

            return new GenerateInsightsResponse
            {
                Success = true,
                InsightType = "metrics",
                ItemsAnalyzed = metrics.Count,
                Insights = insights
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate metrics insights for agent {AgentId} version {VersionId}",
                agentId, versionId);
            return new GenerateInsightsResponse
            {
                Success = false,
                InsightType = "metrics",
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Generates insights from sentiment data.
    /// </summary>
    public async Task<GenerateInsightsResponse> GenerateSentimentInsightsAsync(
        string agentId,
        int versionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

            // Get sentiment records for messages from this version
            // We need both the assistant message and user sentiment
            List<MessageSentiment> sentiments = await context.MessageSentiments
                .Include(s => s.Message)
                .Where(s => s.Message != null && s.Message.InstructionVersionId == versionId)
                .OrderByDescending(s => s.CreatedAt)
                .Take(MaxItemsToAnalyze)
                .ToListAsync(cancellationToken);

            if (sentiments.Count == 0)
            {
                return new GenerateInsightsResponse
                {
                    Success = true,
                    InsightType = "sentiment",
                    ItemsAnalyzed = 0,
                    Insights = "No sentiment data available for analysis."
                };
            }

            // Get the current prompt for context
            string currentPrompt = await GetCurrentPromptAsync(context, versionId, cancellationToken);

            // Build the user message with sentiment data
            string userMessage = BuildSentimentInsightsPrompt(sentiments, currentPrompt);

            // Call the AI
            string insights = await GetAiResponseAsync(
                PromptImproverSystemPrompts.SentimentInsightsPrompt,
                userMessage,
                cancellationToken);

            return new GenerateInsightsResponse
            {
                Success = true,
                InsightType = "sentiment",
                ItemsAnalyzed = sentiments.Count,
                Insights = insights
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate sentiment insights for agent {AgentId} version {VersionId}",
                agentId, versionId);
            return new GenerateInsightsResponse
            {
                Success = false,
                InsightType = "sentiment",
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Generates an improved prompt based on all collected insights.
    /// </summary>
    public async Task<GenerateImprovedPromptResponse> GenerateImprovedPromptAsync(
        string agentId,
        int versionId,
        GenerateImprovedPromptRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Build the user message with all inputs
            string userMessage = BuildImprovedPromptInput(request);

            // Call the AI
            string improvedPrompt = await GetAiResponseAsync(
                PromptImproverSystemPrompts.PromptGenerationPrompt,
                userMessage,
                cancellationToken);

            return new GenerateImprovedPromptResponse
            {
                Success = true,
                ImprovedPrompt = improvedPrompt.Trim()
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate improved prompt for agent {AgentId} version {VersionId}",
                agentId, versionId);
            return new GenerateImprovedPromptResponse
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    #region Prompt Builder Methods (Public for Testing)

    /// <summary>
    /// Builds the prompt for feedback insights generation.
    /// </summary>
    public static string BuildFeedbackInsightsPrompt(List<MessageFeedback> feedbackItems, string currentPrompt)
    {
        StringBuilder sb = new();
        sb.AppendLine("## Current Agent Prompt");
        sb.AppendLine(currentPrompt);
        sb.AppendLine();
        sb.AppendLine("## User Feedback Data");
        sb.AppendLine();

        foreach (MessageFeedback feedback in feedbackItems)
        {
            string sentiment = feedback.IsPositive ? "👍 Positive" : "👎 Negative";
            sb.AppendLine($"- {sentiment}: {feedback.Comment ?? "(no comment)"}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Builds the prompt for metrics insights generation.
    /// </summary>
    public static string BuildMetricsInsightsPrompt(List<MessageEvaluationMetric> metrics, string currentPrompt)
    {
        StringBuilder sb = new();
        sb.AppendLine("## Current Agent Prompt");
        sb.AppendLine(currentPrompt);
        sb.AppendLine();
        sb.AppendLine("## Evaluation Metrics Summary");
        sb.AppendLine();

        // Group by metric name and calculate averages
        var metricGroups = metrics
            .GroupBy(m => m.MetricName)
            .Select(g => new
            {
                Name = g.Key,
                Average = g.Average(m => m.Score),
                Count = g.Count()
            })
            .OrderBy(g => g.Average);

        foreach (var group in metricGroups)
        {
            sb.AppendLine($"- {group.Name}: Average {group.Average:F2} (from {group.Count} evaluations)");
        }

        // Also include some specific low-scoring remarks
        var lowScoring = metrics
            .Where(m => m.Score < 3 && !string.IsNullOrEmpty(m.Remarks))
            .Take(10)
            .ToList();

        if (lowScoring.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Notable Low-Score Remarks");
            foreach (var metric in lowScoring)
            {
                sb.AppendLine($"- {metric.MetricName} ({metric.Score:F1}): {metric.Remarks}");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Builds the prompt for sentiment insights generation.
    /// </summary>
    public static string BuildSentimentInsightsPrompt(List<MessageSentiment> sentiments, string currentPrompt)
    {
        StringBuilder sb = new();
        sb.AppendLine("## Current Agent Prompt");
        sb.AppendLine(currentPrompt);
        sb.AppendLine();
        sb.AppendLine("## Assistant Messages with User Sentiment");
        sb.AppendLine();

        foreach (MessageSentiment sentiment in sentiments)
        {
            string sentimentLabel = sentiment.Sentiment switch
            {
                1 => "😊 Positive",
                -1 => "😞 Negative",
                _ => "😐 Neutral"
            };

            string messagePreview = sentiment.Message?.Text ?? "(message not available)";
            if (messagePreview.Length > 200)
            {
                messagePreview = messagePreview[..200] + "...";
            }

            sb.AppendLine($"**{sentimentLabel}** (confidence: {sentiment.Confidence:P0})");
            sb.AppendLine($"> {messagePreview}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Builds the input for improved prompt generation.
    /// </summary>
    public static string BuildImprovedPromptInput(GenerateImprovedPromptRequest request)
    {
        StringBuilder sb = new();
        sb.AppendLine("## Current Prompt to Improve");
        sb.AppendLine(request.CurrentPrompt);
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(request.UserFeedback))
        {
            sb.AppendLine("## User's Specific Requests");
            sb.AppendLine(request.UserFeedback);
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(request.FeedbackInsights))
        {
            sb.AppendLine("## Insights from User Feedback");
            sb.AppendLine(request.FeedbackInsights);
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(request.MetricsInsights))
        {
            sb.AppendLine("## Insights from Evaluation Metrics");
            sb.AppendLine(request.MetricsInsights);
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(request.SentimentInsights))
        {
            sb.AppendLine("## Insights from Sentiment Analysis");
            sb.AppendLine(request.SentimentInsights);
            sb.AppendLine();
        }

        sb.AppendLine("Generate an improved version of the prompt that addresses the insights above.");

        return sb.ToString();
    }

    #endregion

    #region Private Helpers

    private async Task<string> GetCurrentPromptAsync(
        JaimesDbContext context,
        int versionId,
        CancellationToken cancellationToken)
    {
        AgentInstructionVersion? version = await context.AgentInstructionVersions
            .FirstOrDefaultAsync(v => v.Id == versionId, cancellationToken);

        return version?.Instructions ?? "(prompt not found)";
    }

    private async Task<string> GetAiResponseAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken cancellationToken)
    {
        List<ChatMessage> messages =
        [
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.User, userMessage)
        ];

        ChatResponse response = await chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
        return response.Messages?.FirstOrDefault()?.Text ?? string.Empty;
    }

    #endregion
}
