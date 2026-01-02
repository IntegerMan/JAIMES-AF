using MattEland.Jaimes.ApiService.Services;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Requests;

namespace MattEland.Jaimes.Tests.Services;

/// <summary>
/// Tests for PromptImproverService prompt building methods.
/// </summary>
public class PromptImproverServiceTests
{
    [Fact]
    public void BuildFeedbackInsightsPrompt_WithFeedbackItems_ReturnsValidPrompt()
    {
        // Arrange
        var feedbackItems = new List<MessageFeedback>
        {
            new() { IsPositive = true, Comment = "Great response!" },
            new() { IsPositive = false, Comment = "Too long" }
        };
        const string currentPrompt = "You are a helpful AI assistant.";

        // Act
        string result = PromptImproverService.BuildFeedbackInsightsPrompt(feedbackItems, currentPrompt);

        // Assert
        Assert.Contains("Current Agent Prompt", result, StringComparison.Ordinal);
        Assert.Contains(currentPrompt, result, StringComparison.Ordinal);
        Assert.Contains("User Feedback Data", result, StringComparison.Ordinal);
        Assert.Contains("Positive", result, StringComparison.Ordinal);
        Assert.Contains("Negative", result, StringComparison.Ordinal);
        Assert.Contains("Great response!", result, StringComparison.Ordinal);
        Assert.Contains("Too long", result, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildMetricsInsightsPrompt_WithMetricScores_ReturnsValidPrompt()
    {
        // Arrange
        var metrics = new List<MessageEvaluationMetric>
        {
            new() { MetricName = "Brevity", Score = 4.5, Remarks = "Good length" },
            new() { MetricName = "Brevity", Score = 2.0, Remarks = "Too verbose" },
            new() { MetricName = "Relevance", Score = 5.0, Remarks = "Excellent" }
        };
        const string currentPrompt = "You are a helpful AI assistant.";

        // Act
        string result = PromptImproverService.BuildMetricsInsightsPrompt(metrics, currentPrompt);

        // Assert
        Assert.Contains("Current Agent Prompt", result, StringComparison.Ordinal);
        Assert.Contains(currentPrompt, result, StringComparison.Ordinal);
        Assert.Contains("Evaluation Metrics Summary", result, StringComparison.Ordinal);
        Assert.Contains("Brevity", result, StringComparison.Ordinal);
        Assert.Contains("Relevance", result, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildMetricsInsightsPrompt_WithLowScores_IncludesRemarks()
    {
        // Arrange
        var metrics = new List<MessageEvaluationMetric>
        {
            new() { MetricName = "Clarity", Score = 1.0, Remarks = "Very confusing response" },
            new() { MetricName = "Clarity", Score = 5.0, Remarks = "Perfect" }
        };
        const string currentPrompt = "You are a helpful AI assistant.";

        // Act
        string result = PromptImproverService.BuildMetricsInsightsPrompt(metrics, currentPrompt);

        // Assert
        Assert.Contains("Notable Low-Score Remarks", result, StringComparison.Ordinal);
        Assert.Contains("Clarity (1.0): Very confusing response", result, StringComparison.Ordinal);
        Assert.DoesNotContain("Perfect", result, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSentimentInsightsPrompt_WithSentimentPairs_ReturnsValidPrompt()
    {
        // Arrange
        var sentiments = new List<MessageSentiment>
        {
            new()
            {
                Sentiment = 1,
                Confidence = 0.95,
                Message = new Message
                {
                    Text = "This is a positive message from the assistant.", AgentId = "test-agent",
                    InstructionVersionId = 1
                }
            },
            new()
            {
                Sentiment = -1,
                Confidence = 0.80,
                Message = new Message
                    { Text = "This response upset the user.", AgentId = "test-agent", InstructionVersionId = 1 }
            }
        };
        const string currentPrompt = "You are a helpful AI assistant.";

        // Act
        string result = PromptImproverService.BuildSentimentInsightsPrompt(sentiments, currentPrompt);

        // Assert
        Assert.Contains("Current Agent Prompt", result, StringComparison.Ordinal);
        Assert.Contains(currentPrompt, result, StringComparison.Ordinal);
        Assert.Contains("Assistant Messages with User Sentiment", result, StringComparison.Ordinal);
        Assert.Contains("Positive", result, StringComparison.Ordinal);
        Assert.Contains("Negative", result, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSentimentInsightsPrompt_WithNeutralSentiment_ReturnsValidPrompt()
    {
        // Arrange
        var sentiments = new List<MessageSentiment>
        {
            new()
            {
                Sentiment = 0,
                Confidence = 0.50,
                Message = new Message { Text = "Neutral response.", AgentId = "test-agent", InstructionVersionId = 1 }
            }
        };
        const string currentPrompt = "You are a helpful AI assistant.";

        // Act
        string result = PromptImproverService.BuildSentimentInsightsPrompt(sentiments, currentPrompt);

        // Assert
        Assert.Contains("Neutral", result, StringComparison.Ordinal);
        Assert.Contains("Neutral response.", result, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildImprovedPromptInput_WithAllInsights_CombinesCorrectly()
    {
        // Arrange
        var request = new GenerateImprovedPromptRequest
        {
            CurrentPrompt = "You are a helpful AI assistant.",
            UserFeedback = "Make responses shorter",
            FeedbackInsights = "Users want brevity",
            MetricsInsights = "Low scores on response length",
            SentimentInsights = "Negative sentiment when verbose"
        };

        // Act
        string result = PromptImproverService.BuildImprovedPromptInput(request);

        // Assert
        Assert.Contains("Current Prompt to Improve", result, StringComparison.Ordinal);
        Assert.Contains(request.CurrentPrompt, result, StringComparison.Ordinal);
        Assert.Contains("User's Specific Requests", result, StringComparison.Ordinal);
        Assert.Contains("Make responses shorter", result, StringComparison.Ordinal);
        Assert.Contains("Insights from User Feedback", result, StringComparison.Ordinal);
        Assert.Contains("Insights from Evaluation Metrics", result, StringComparison.Ordinal);
        Assert.Contains("Insights from Sentiment Analysis", result, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildImprovedPromptInput_WithPartialInsights_HandlesNulls()
    {
        // Arrange
        var request = new GenerateImprovedPromptRequest
        {
            CurrentPrompt = "You are a helpful AI assistant.",
            // Deliberately leaving other fields null
        };

        // Act
        string result = PromptImproverService.BuildImprovedPromptInput(request);

        // Assert
        Assert.Contains("Current Prompt to Improve", result, StringComparison.Ordinal);
        Assert.Contains(request.CurrentPrompt, result, StringComparison.Ordinal);
        // Should NOT contain sections for null fields
        Assert.DoesNotContain("User's Specific Requests", result, StringComparison.Ordinal);
        Assert.DoesNotContain("Insights from User Feedback", result, StringComparison.Ordinal);
        Assert.DoesNotContain("Insights from Evaluation Metrics", result, StringComparison.Ordinal);
        Assert.DoesNotContain("Insights from Sentiment Analysis", result, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildImprovedPromptInput_WithManualInstructions_UsesManualInstructions()
    {
        // Arrange
        var request = new GenerateImprovedPromptRequest
        {
            CurrentPrompt = "You are a helpful AI assistant.",
            ManualInstructions = "Use pirate speech for everything.",
            UserFeedback = "Make responses shorter"
        };

        // Act
        string result = PromptImproverService.BuildImprovedPromptInput(request);

        // Assert
        Assert.Equal(request.ManualInstructions, result);
        Assert.DoesNotContain(request.CurrentPrompt, result, StringComparison.Ordinal);
        Assert.DoesNotContain("User's Specific Requests", result, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildToolUsageInsightsPrompt_WithToolData_ReturnsValidPrompt()
    {
        // Arrange
        var toolCalls = new List<MessageToolCall>
        {
            new() { ToolName = "SearchTool", CreatedAt = DateTime.UtcNow },
            new() { ToolName = "SearchTool", CreatedAt = DateTime.UtcNow.AddMinutes(-5) },
            new() { ToolName = "WeatherTool", CreatedAt = DateTime.UtcNow.AddMinutes(-10) }
        };
        var registeredTools = new List<Tool>
        {
            new() { Name = "SearchTool", Description = "Searches for information" },
            new() { Name = "WeatherTool", Description = "Gets weather data" },
            new() { Name = "CalendarTool", Description = "Manages calendar events" }
        };
        var assistantMessages = new List<Message>
        {
            new()
            {
                Id = 1, Text = "Here's the weather forecast.", AgentId = "test-agent", InstructionVersionId = 1,
                CreatedAt = DateTime.UtcNow
            }
        };
        const string currentPrompt = "You are a helpful AI assistant.";

        // Act
        string result =
            PromptImproverService.BuildToolUsageInsightsPrompt(toolCalls, registeredTools, assistantMessages,
                currentPrompt);

        // Assert
        Assert.Contains("Current Agent Prompt", result, StringComparison.Ordinal);
        Assert.Contains(currentPrompt, result, StringComparison.Ordinal);
        Assert.Contains("Available Tools", result, StringComparison.Ordinal);
        Assert.Contains("Tool Usage Statistics", result, StringComparison.Ordinal);
        Assert.Contains("SearchTool", result, StringComparison.Ordinal);
        Assert.Contains("Called 2 time(s)", result, StringComparison.Ordinal);
        Assert.Contains("CalendarTool", result, StringComparison.Ordinal);
        Assert.Contains("Sample Assistant Messages", result, StringComparison.Ordinal);
        Assert.Contains("weather forecast", result, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildImprovedPromptInput_WithToolInsights_IncludesSection()
    {
        // Arrange
        var request = new GenerateImprovedPromptRequest
        {
            CurrentPrompt = "You are a helpful AI assistant.",
            ToolInsights = "Agent is not using the CalendarTool when appropriate"
        };

        // Act
        string result = PromptImproverService.BuildImprovedPromptInput(request);

        // Assert
        Assert.Contains("Insights from Tool Usage", result, StringComparison.Ordinal);
        Assert.Contains("CalendarTool", result, StringComparison.Ordinal);
    }
}
