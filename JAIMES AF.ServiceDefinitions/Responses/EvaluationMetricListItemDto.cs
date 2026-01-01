namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// DTO for evaluation metrics list items, including context information.
/// </summary>
public class EvaluationMetricListItemDto
{
    /// <summary>
    /// The unique identifier for this evaluation metric.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The message ID this metric is associated with.
    /// </summary>
    public int MessageId { get; set; }

    /// <summary>
    /// The name of the metric (e.g., "Relevance", "Truth", "Completeness").
    /// </summary>
    public required string MetricName { get; set; }

    /// <summary>
    /// The evaluator ID this metric is associated with.
    /// </summary>
    public int? EvaluatorId { get; set; }

    /// <summary>
    /// The name of the evaluator this metric is associated with.
    /// </summary>
    public string? EvaluatorName { get; set; }

    /// <summary>
    /// The numeric score for this metric.
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// Whether this metric passed (derived from score threshold >= 3).
    /// </summary>
    public bool Passed { get; set; }

    /// <summary>
    /// Evaluation remarks or reasoning.
    /// </summary>
    public string? Remarks { get; set; }

    /// <summary>
    /// Diagnostic data as a JSON string.
    /// </summary>
    public string? Diagnostics { get; set; }

    /// <summary>
    /// When the evaluation was performed.
    /// </summary>
    public DateTime EvaluatedAt { get; set; }

    // Context information

    /// <summary>
    /// The game ID associated with this metric's message.
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// The player name for the game.
    /// </summary>
    public string? GamePlayerName { get; set; }

    /// <summary>
    /// The scenario name for the game.
    /// </summary>
    public string? GameScenarioName { get; set; }

    /// <summary>
    /// The ruleset ID for the game.
    /// </summary>
    public string? GameRulesetId { get; set; }

    /// <summary>
    /// The agent instruction version number.
    /// </summary>
    public string? AgentVersion { get; set; }

    /// <summary>
    /// A short preview of the message content.
    /// </summary>
    public string? MessagePreview { get; set; }

    /// <summary>
    /// True if this is a placeholder for a missing evaluator.
    /// </summary>
    public bool IsMissing { get; set; }
}
