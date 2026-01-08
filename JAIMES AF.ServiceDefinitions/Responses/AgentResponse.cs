namespace MattEland.Jaimes.ServiceDefinitions.Responses;

public record AgentResponse
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Role { get; init; }

    public int VersionCount { get; init; }
    public int FeedbackPositiveCount { get; init; }
    public int FeedbackNegativeCount { get; init; }
    public int SentimentPositiveCount { get; init; }
    public int SentimentNeutralCount { get; init; }
    public int SentimentNegativeCount { get; init; }
    public List<AgentEvaluatorMetricSummaryDto> EvaluationMetrics { get; init; } = [];
}



