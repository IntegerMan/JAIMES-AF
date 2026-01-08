namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Response containing aggregate evaluation statistics for a filtered set of messages.
/// </summary>
public record MessagesAggregateStatsResponse
{
    /// <summary>
    /// The total number of messages matching the filter.
    /// </summary>
    public int MessageCount { get; init; }

    /// <summary>
    /// The total number of AI messages matching the filter.
    /// </summary>
    public int AiMessageCount { get; init; }

    /// <summary>
    /// Aggregate evaluation metrics grouped by evaluator.
    /// </summary>
    public List<AgentEvaluatorMetricSummaryDto> EvaluationMetrics { get; init; } = [];
}
