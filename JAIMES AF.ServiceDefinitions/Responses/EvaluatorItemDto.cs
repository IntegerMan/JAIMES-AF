namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Represents an evaluator with aggregate statistics.
/// </summary>
public class EvaluatorItemDto
{
    /// <summary>
    /// Gets or sets the unique identifier for this evaluator.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the evaluator (matches the metric name).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the evaluator.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets when the evaluator was first registered.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the total number of metrics produced by this evaluator.
    /// </summary>
    public int MetricCount { get; set; }

    /// <summary>
    /// Gets or sets the average score across all metrics for this evaluator.
    /// </summary>
    public double? AverageScore { get; set; }

    /// <summary>
    /// Gets or sets the number of passing metrics.
    /// </summary>
    public int PassCount { get; set; }

    /// <summary>
    /// Gets or sets the number of failing metrics.
    /// </summary>
    public int FailCount { get; set; }
}
