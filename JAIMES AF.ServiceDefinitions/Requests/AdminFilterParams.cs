namespace MattEland.Jaimes.ServiceDefinitions.Requests;

/// <summary>
/// Centralized filter parameters used across admin list endpoints.
/// </summary>
public class AdminFilterParams
{
    /// <summary>
    /// Filter by specific game ID.
    /// </summary>
    public Guid? GameId { get; set; }

    /// <summary>
    /// Filter by agent identifier.
    /// </summary>
    public string? AgentId { get; set; }

    /// <summary>
    /// Filter by agent instruction version ID.
    /// </summary>
    public int? InstructionVersionId { get; set; }

    /// <summary>
    /// Filter by tool name.
    /// </summary>
    public string? ToolName { get; set; }

    /// <summary>
    /// Filter by feedback type (positive/negative).
    /// </summary>
    public bool? IsPositive { get; set; }

    /// <summary>
    /// Filter by evaluation metric name.
    /// </summary>
    public string? MetricName { get; set; }

    /// <summary>
    /// Filter by minimum metric score.
    /// </summary>
    public double? MinScore { get; set; }

    /// <summary>
    /// Filter by maximum metric score.
    /// </summary>
    public double? MaxScore { get; set; }

    /// <summary>
    /// Filter by pass/fail status.
    /// </summary>
    public bool? Passed { get; set; }

    /// <summary>
    /// Filter by sentiment value: -1 (negative), 0 (neutral), 1 (positive).
    /// </summary>
    public int? Sentiment { get; set; }

    /// <summary>
    /// Filter by feedback type on the AI response: -1 (negative), 0 (neutral - returns no results), 1 (positive), null for any.
    /// Use with HasFeedback to filter for messages with or without feedback.
    /// </summary>
    public int? FeedbackType { get; set; }

    /// <summary>
    /// Filter by evaluator ID.
    /// </summary>
    public int? EvaluatorId { get; set; }

    /// <summary>
    /// Filter by whether the AI response has any feedback.
    /// </summary>
    public bool? HasFeedback { get; set; }
}
