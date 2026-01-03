namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Response from running evaluators on a test AI response.
/// </summary>
public record TestEvaluatorResponse
{
    /// <summary>
    /// The evaluation results for each metric.
    /// </summary>
    public List<TestEvaluatorMetricResult> Metrics { get; init; } = [];

    /// <summary>
    /// The total execution time in milliseconds.
    /// </summary>
    public long ExecutionTimeMs { get; init; }

    /// <summary>
    /// Any errors that occurred during evaluation.
    /// </summary>
    public List<string> Errors { get; init; } = [];

    /// <summary>
    /// The system prompt that was used for evaluation.
    /// </summary>
    public string? SystemPromptUsed { get; init; }

    /// <summary>
    /// The ruleset name that was used (if applicable).
    /// </summary>
    public string? RulesetNameUsed { get; init; }
}

/// <summary>
/// Represents the result of a single evaluation metric.
/// </summary>
public record TestEvaluatorMetricResult
{
    /// <summary>
    /// The name of the metric.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The evaluator that produced this metric.
    /// </summary>
    public required string EvaluatorName { get; init; }

    /// <summary>
    /// The numeric score (if applicable).
    /// </summary>
    public double? Score { get; init; }

    /// <summary>
    /// Whether the evaluation passed.
    /// </summary>
    public bool? Passed { get; init; }

    /// <summary>
    /// The reason or explanation for the score.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Diagnostic messages from the evaluation.
    /// </summary>
    public List<TestEvaluatorDiagnostic> Diagnostics { get; init; } = [];
}

/// <summary>
/// A diagnostic message from an evaluation.
/// </summary>
public record TestEvaluatorDiagnostic
{
    /// <summary>
    /// The severity of the diagnostic (Informational, Warning, Error).
    /// </summary>
    public required string Severity { get; init; }

    /// <summary>
    /// The diagnostic message.
    /// </summary>
    public required string Message { get; init; }
}
