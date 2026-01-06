namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Response containing consolidated statistics for a specific agent or agent version.
/// </summary>
public record AgentStatsResponse
{
    public int MessageCount { get; init; }
    public int FeedbackPositiveCount { get; init; }
    public int FeedbackNegativeCount { get; init; }
    public int SentimentPositiveCount { get; init; }
    public int SentimentNeutralCount { get; init; }
    public int SentimentNegativeCount { get; init; }
    public int ToolCallCount { get; init; }
    public List<AgentEvaluatorMetricSummaryDto> EvaluationMetrics { get; init; } = [];
}

public record AgentEvaluatorMetricSummaryDto
{
    public required string MetricName { get; init; }
    public double AverageScore { get; init; }
}
