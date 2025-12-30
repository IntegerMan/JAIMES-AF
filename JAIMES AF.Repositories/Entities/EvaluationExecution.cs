namespace MattEland.Jaimes.Repositories.Entities;

/// <summary>
/// Represents a single evaluation execution, which can contain multiple scenario iterations.
/// </summary>
[Table("EvaluationExecutions")]
public class EvaluationExecution
{
    /// <summary>
    /// Gets or sets the unique name for this evaluation execution.
    /// </summary>
    [Key]
    [Required]
    [MaxLength(250)]
    public string ExecutionName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when this execution occurred.
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property to the scenario iterations associated with this execution.
    /// </summary>
    public ICollection<EvaluationScenarioIteration> ScenarioIterations { get; set; } =
        new List<EvaluationScenarioIteration>();
}
