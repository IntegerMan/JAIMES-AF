namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Response containing statistics for a specific evaluator with optional filtering.
/// </summary>
public record EvaluatorStatsResponse
{
    /// <summary>
    /// The evaluator's unique identifier.
    /// </summary>
    public int EvaluatorId { get; init; }

    /// <summary>
    /// The evaluator's name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The evaluator's description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// The total number of evaluations performed by this evaluator.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// The number of evaluations that passed (score >= 3).
    /// </summary>
    public int PassCount { get; init; }

    /// <summary>
    /// The number of evaluations that failed (score &lt; 3).
    /// </summary>
    public int FailCount { get; init; }

    /// <summary>
    /// The average score across all evaluations.
    /// </summary>
    public double? AverageScore { get; init; }

    /// <summary>
    /// Score distribution histogram - count of evaluations at each score level (1-5).
    /// </summary>
    public ScoreDistribution ScoreDistribution { get; init; } = new();

    /// <summary>
    /// When the evaluator was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// Score distribution histogram data showing count of evaluations in each bucket.
/// </summary>
public record ScoreDistribution
{
    /// <summary>Count of evaluations with score >= 1 and &lt; 2.</summary>
    public int Score1 { get; init; }

    /// <summary>Count of evaluations with score >= 2 and &lt; 3.</summary>
    public int Score2 { get; init; }

    /// <summary>Count of evaluations with score >= 3 and &lt; 4.</summary>
    public int Score3 { get; init; }

    /// <summary>Count of evaluations with score >= 4 and &lt; 5.</summary>
    public int Score4 { get; init; }

    /// <summary>Count of evaluations with score >= 5.</summary>
    public int Score5 { get; init; }
}
