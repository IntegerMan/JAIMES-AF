namespace MattEland.Jaimes.Repositories.Entities;

/// <summary>
/// Represents a single iteration of an evaluation scenario within an execution.
/// </summary>
[Table("EvaluationScenarioIterations")]
public class EvaluationScenarioIteration
{
    /// <summary>
    /// Gets or sets the name of the execution this iteration belongs to.
    /// </summary>
    [Required]
    [MaxLength(250)]
    public string ExecutionName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the scenario.
    /// </summary>
    [Required]
    [MaxLength(250)]
    public string ScenarioName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the iteration.
    /// </summary>
    [Required]
    [MaxLength(250)]
    public string IterationName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the JSON representation of the evaluation result.
    /// </summary>
    [Required]
    [Column(TypeName = "jsonb")]
    public string ResultJson { get; set; } = string.Empty;

    /// <summary>
    /// Navigation property to the execution this iteration belongs to.
    /// </summary>
    [ForeignKey(nameof(ExecutionName))]
    public EvaluationExecution? Execution { get; set; }
}
