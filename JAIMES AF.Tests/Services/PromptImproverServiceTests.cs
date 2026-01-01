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
}
