using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MattEland.Jaimes.Repositories.Entities;

/// <summary>
/// Represents an evaluation metric for an assistant message.
/// Stores individual metrics (Relevance, Truth, Completeness) from AI evaluation.
/// </summary>
[Table("MessageEvaluationMetrics")]
public class MessageEvaluationMetric
{
    /// <summary>
    /// Gets or sets the unique identifier for this evaluation metric (auto-incrementing).
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the ID of the message this evaluation metric belongs to.
    /// </summary>
    [Required]
    public int MessageId { get; set; }

    /// <summary>
    /// Gets or sets the name of the metric (e.g., "Relevance", "Truth", "Completeness").
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string MetricName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the numeric score for this metric (typically 1-5 for RelevanceTruthAndCompletenessEvaluator).
    /// </summary>
    [Required]
    public double Score { get; set; }

    /// <summary>
    /// Gets or sets the evaluation remarks or reasoning for this metric.
    /// </summary>
    [Column(TypeName = "text")]
    public string? Remarks { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this evaluation was performed.
    /// </summary>
    [Required]
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the diagnostic data as a JSON string.
    /// Contains additional evaluation context and metadata.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string? Diagnostics { get; set; }

    /// <summary>
    /// Gets or sets the ID of the model used to perform this evaluation.
    /// </summary>
    public int? EvaluationModelId { get; set; }

    /// <summary>
    /// Navigation property to the model used to perform this evaluation.
    /// </summary>
    [ForeignKey(nameof(EvaluationModelId))]
    public Model? EvaluationModel { get; set; }

    /// <summary>
    /// Navigation property to the message this evaluation metric belongs to.
    /// </summary>
    [ForeignKey(nameof(MessageId))]
    public Message? Message { get; set; }
}
