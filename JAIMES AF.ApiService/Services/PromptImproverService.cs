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
    private const int MessageBatchSize = 20; // Messages per batch
    private const int MaxMessageBatches = 5; // Maximum batches to analyze (100 messages total)

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
            "messages" => await GenerateMessageInsightsAsync(agentId, versionId, cancellationToken),
            "tools" => await GenerateToolUsageInsightsAsync(agentId, versionId, cancellationToken),
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

            string trimmedInsights = insights.Trim();
            if (string.IsNullOrWhiteSpace(trimmedInsights))
            {
                return new GenerateInsightsResponse
                {
                    Success = false,
                    InsightType = "feedback",
                    Error = "The AI returned an empty response for feedback insights. Please try again."
                };
            }

            return new GenerateInsightsResponse
            {
                Success = true,
                InsightType = "feedback",
                ItemsAnalyzed = feedbackItems.Count,
                Insights = trimmedInsights
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

            string trimmedInsights = insights.Trim();
            if (string.IsNullOrWhiteSpace(trimmedInsights))
            {
                return new GenerateInsightsResponse
                {
                    Success = false,
                    InsightType = "metrics",
                    Error = "The AI returned an empty response for metrics insights. Please try again."
                };
            }

            return new GenerateInsightsResponse
            {
                Success = true,
                InsightType = "metrics",
                ItemsAnalyzed = metrics.Count,
                Insights = trimmedInsights
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

            string trimmedInsights = insights.Trim();
            if (string.IsNullOrWhiteSpace(trimmedInsights))
            {
                return new GenerateInsightsResponse
                {
                    Success = false,
                    InsightType = "sentiment",
                    Error = "The AI returned an empty response for sentiment insights. Please try again."
                };
            }

            return new GenerateInsightsResponse
            {
                Success = true,
                InsightType = "sentiment",
                ItemsAnalyzed = sentiments.Count,
                Insights = trimmedInsights
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
    /// Generates insights from conversation message fragments.
    /// Analyzes messages in batches, then summarizes the batch insights.
    /// </summary>
    public async Task<GenerateInsightsResponse> GenerateMessageInsightsAsync(
        string agentId,
        int versionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

            // Get messages for this agent version
            List<Message> messages = await context.Messages
                .Where(m => m.AgentId == agentId && m.InstructionVersionId == versionId)
                .OrderByDescending(m => m.CreatedAt)
                .Take(MessageBatchSize * MaxMessageBatches)
                .ToListAsync(cancellationToken);

            if (messages.Count == 0)
            {
                return new GenerateInsightsResponse
                {
                    Success = true,
                    InsightType = "messages",
                    ItemsAnalyzed = 0,
                    Insights = "No message data available for analysis."
                };
            }

            // Get the current prompt for context
            string currentPrompt = await GetCurrentPromptAsync(context, versionId, cancellationToken);

            // Split messages into batches
            List<List<Message>> batches = SplitIntoBatches(messages, MessageBatchSize);

            // Analyze each batch in parallel
            List<Task<string>> batchTasks = batches
                .Select(batch => AnalyzeMessageBatchAsync(batch, currentPrompt, cancellationToken))
                .ToList();

            string[] batchInsights = await Task.WhenAll(batchTasks);

            // If only one batch, return its insights directly
            if (batchInsights.Length == 1)
            {
                string insights = batchInsights[0].Trim();
                if (string.IsNullOrWhiteSpace(insights))
                {
                    return new GenerateInsightsResponse
                    {
                        Success = false,
                        InsightType = "messages",
                        Error = "The AI returned an empty response for message insights. Please try again."
                    };
                }

                return new GenerateInsightsResponse
                {
                    Success = true,
                    InsightType = "messages",
                    ItemsAnalyzed = messages.Count,
                    Insights = insights
                };
            }

            // Summarize all batch insights into final coaching message
            string finalInsights = await SummarizeBatchInsightsAsync(batchInsights, cancellationToken);

            string trimmedInsights = finalInsights.Trim();
            if (string.IsNullOrWhiteSpace(trimmedInsights))
            {
                return new GenerateInsightsResponse
                {
                    Success = false,
                    InsightType = "messages",
                    Error = "The AI returned an empty response for message insights. Please try again."
                };
            }

            return new GenerateInsightsResponse
            {
                Success = true,
                InsightType = "messages",
                ItemsAnalyzed = messages.Count,
                Insights = trimmedInsights
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate message insights for agent {AgentId} version {VersionId}",
                agentId, versionId);
            return new GenerateInsightsResponse
            {
                Success = false,
                InsightType = "messages",
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Generates insights from tool usage patterns.
    /// Analyzes tool usage appropriateness based on assistant messages and available tools.
    /// </summary>
    public async Task<GenerateInsightsResponse> GenerateToolUsageInsightsAsync(
        string agentId,
        int versionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

            // Get assistant messages for this version (PlayerId is null for assistant messages)
            List<Message> assistantMessages = await context.Messages
                .Where(m => m.InstructionVersionId == versionId && m.PlayerId == null)
                .OrderByDescending(m => m.CreatedAt)
                .Take(MaxItemsToAnalyze)
                .ToListAsync(cancellationToken);

            if (assistantMessages.Count == 0)
            {
                return new GenerateInsightsResponse
                {
                    Success = true,
                    InsightType = "tools",
                    ItemsAnalyzed = 0,
                    Insights = "No assistant messages available for tool usage analysis."
                };
            }

            // Get tool calls for this version
            List<MessageToolCall> toolCalls = await context.MessageToolCalls
                .Include(mtc => mtc.Message)
                .Where(mtc => mtc.InstructionVersionId == versionId ||
                              (mtc.Message != null && mtc.Message.InstructionVersionId == versionId))
                .OrderByDescending(mtc => mtc.CreatedAt)
                .Take(MaxItemsToAnalyze * 2)
                .ToListAsync(cancellationToken);

            // Get all registered tools
            List<Tool> registeredTools = await context.Tools
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            // Get the current prompt for context
            string currentPrompt = await GetCurrentPromptAsync(context, versionId, cancellationToken);

            // Build the user message with tool usage data and message samples
            string userMessage =
                BuildToolUsageInsightsPrompt(toolCalls, registeredTools, assistantMessages, currentPrompt);

            // Call the AI
            string insights = await GetAiResponseAsync(
                PromptImproverSystemPrompts.ToolUsageInsightsPrompt,
                userMessage,
                cancellationToken);

            string trimmedInsights = insights.Trim();
            if (string.IsNullOrWhiteSpace(trimmedInsights))
            {
                return new GenerateInsightsResponse
                {
                    Success = false,
                    InsightType = "tools",
                    Error = "The AI returned an empty response for tool usage insights. Please try again."
                };
            }

            return new GenerateInsightsResponse
            {
                Success = true,
                InsightType = "tools",
                ItemsAnalyzed = assistantMessages.Count,
                Insights = trimmedInsights
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate tool usage insights for agent {AgentId} version {VersionId}",
                agentId, versionId);
            return new GenerateInsightsResponse
            {
                Success = false,
                InsightType = "tools",
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Analyzes a single batch of messages and returns coaching insights.
    /// </summary>
    private async Task<string> AnalyzeMessageBatchAsync(
        List<Message> messages,
        string currentPrompt,
        CancellationToken cancellationToken)
    {
        string userMessage = BuildMessageInsightsPrompt(messages, currentPrompt);

        return await GetAiResponseAsync(
            PromptImproverSystemPrompts.MessageInsightsPrompt,
            userMessage,
            cancellationToken);
    }

    /// <summary>
    /// Summarizes insights from multiple message batches into a final coaching message.
    /// </summary>
    private async Task<string> SummarizeBatchInsightsAsync(
        string[] batchInsights,
        CancellationToken cancellationToken)
    {
        StringBuilder sb = new();
        sb.AppendLine("## Batch Insights to Synthesize");
        sb.AppendLine();

        for (int i = 0; i < batchInsights.Length; i++)
        {
            sb.AppendLine($"### Batch {i + 1}");
            sb.AppendLine(batchInsights[i]);
            sb.AppendLine();
        }

        return await GetAiResponseAsync(
            PromptImproverSystemPrompts.MessageInsightsSummaryPrompt,
            sb.ToString(),
            cancellationToken);
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

            string trimmedPrompt = improvedPrompt.Trim();
            if (string.IsNullOrWhiteSpace(trimmedPrompt))
            {
                return new GenerateImprovedPromptResponse
                {
                    Success = false,
                    Error = "The AI returned an empty response when generating the improved prompt. Please try again."
                };
            }

            return new GenerateImprovedPromptResponse
            {
                Success = true,
                ImprovedPrompt = trimmedPrompt
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
    /// Builds the prompt for message insights generation.
    /// </summary>
    public static string BuildMessageInsightsPrompt(List<Message> messages, string currentPrompt)
    {
        StringBuilder sb = new();
        sb.AppendLine("## Current Agent Prompt");
        sb.AppendLine(currentPrompt);
        sb.AppendLine();
        sb.AppendLine("## Conversation Fragments");
        sb.AppendLine();

        foreach (Message message in messages)
        {
            sb.AppendLine("---");
            sb.AppendLine($"**Message {message.Id}** ({message.CreatedAt:yyyy-MM-dd HH:mm})");
            sb.AppendLine();
            sb.AppendLine(message.Text);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Builds the prompt for tool usage insights generation.
    /// Includes assistant message samples for tool appropriateness analysis.
    /// </summary>
    public static string BuildToolUsageInsightsPrompt(
        List<MessageToolCall> toolCalls,
        List<Tool> registeredTools,
        List<Message> assistantMessages,
        string currentPrompt)
    {
        StringBuilder sb = new();
        sb.AppendLine("## Current Agent Prompt");
        sb.AppendLine(currentPrompt);
        sb.AppendLine();

        // List all available tools
        if (registeredTools.Count > 0)
        {
            sb.AppendLine("## Available Tools");
            sb.AppendLine();
            foreach (Tool tool in registeredTools)
            {
                sb.AppendLine($"- **{tool.Name}**: {tool.Description ?? "(no description)"}");
            }

            sb.AppendLine();
        }

        // Group tool calls by name and get statistics
        if (toolCalls.Count > 0)
        {
            var toolStats = toolCalls
                .GroupBy(tc => tc.ToolName, StringComparer.OrdinalIgnoreCase)
                .Select(g => new
                {
                    Name = g.Key,
                    Count = g.Count(),
                    LastUsed = g.Max(tc => tc.CreatedAt)
                })
                .OrderByDescending(x => x.Count)
                .ToList();

            sb.AppendLine("## Tool Usage Statistics");
            sb.AppendLine();
            foreach (var stat in toolStats)
            {
                sb.AppendLine($"- **{stat.Name}**: Called {stat.Count} time(s)");
            }

            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("## Tool Usage Statistics");
            sb.AppendLine();
            sb.AppendLine("*No tools have been called by this agent version.*");
            sb.AppendLine();
        }

        // Include sample assistant messages for context
        sb.AppendLine("## Sample Assistant Messages (for tool usage context)");
        sb.AppendLine();
        foreach (Message message in assistantMessages.Take(10))
        {
            sb.AppendLine("---");
            sb.AppendLine($"**Message {message.Id}** ({message.CreatedAt:yyyy-MM-dd HH:mm})");
            sb.AppendLine();
            // Truncate long messages to keep prompt reasonable
            string text = message.Text ?? "";
            if (text.Length > 500)
            {
                text = string.Concat(text.AsSpan(0, 500), "...");
            }

            sb.AppendLine(text);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Builds the input for improved prompt generation.
    /// </summary>
    public static string BuildImprovedPromptInput(GenerateImprovedPromptRequest request)
    {
        // If the user provided manual instructions (e.g. they edited the combined prompt), use that directly
        if (!string.IsNullOrWhiteSpace(request.ManualInstructions))
        {
            return request.ManualInstructions;
        }

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

        if (!string.IsNullOrWhiteSpace(request.MessageInsights))
        {
            sb.AppendLine("## Insights from Conversation Messages");
            sb.AppendLine(request.MessageInsights);
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(request.ToolInsights))
        {
            sb.AppendLine("## Insights from Tool Usage");
            sb.AppendLine(request.ToolInsights);
            sb.AppendLine();
        }

        sb.AppendLine("Generate an improved version of the prompt that addresses the insights above.");

        return sb.ToString();
    }

    /// <summary>
    /// Splits a list into batches of specified size.
    /// </summary>
    private static List<List<T>> SplitIntoBatches<T>(List<T> items, int batchSize)
    {
        List<List<T>> batches = new();

        for (int i = 0; i < items.Count; i += batchSize)
        {
            batches.Add(items.Skip(i).Take(batchSize).ToList());
        }

        return batches;
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
