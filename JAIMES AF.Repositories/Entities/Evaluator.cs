using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MattEland.Jaimes.Repositories.Entities;

/// <summary>
/// Represents an evaluator or evaluation metric in the system.
/// These are auto-detected by scanning assemblies for IEvaluator implementations.
/// </summary>
[Table("Evaluators")]
public class Evaluator
{
    /// <summary>
    /// Gets or sets the unique identifier for this evaluator.
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the unique name of the metric (e.g., "Relevance", "Truth", "Completeness", "Brevity").
    /// This is used for matching against MessageEvaluationMetric.MetricName.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the human-readable description of the evaluator or metric.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this evaluator was first registered in the database.
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property to evaluation metrics produced by this evaluator.
    /// </summary>
    public ICollection<MessageEvaluationMetric> EvaluationMetrics { get; set; } = new List<MessageEvaluationMetric>();
}
