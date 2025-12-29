using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MattEland.Jaimes.Repositories.Entities;

/// <summary>
/// Represents an AI model configuration used for text generation or evaluation.
/// </summary>
[Table("Models")]
public class Model
{
    /// <summary>
    /// Gets or sets the unique identifier for this model (auto-incrementing).
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the AI model (e.g., "gpt-4o-mini", "gemma3").
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the provider type (e.g., "Ollama", "AzureOpenAI", "OpenAI").
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the endpoint URL of the model service.
    /// Nullable because some providers may not require an explicit endpoint.
    /// </summary>
    [MaxLength(500)]
    public string? Endpoint { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this model record was first created.
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property for agent instruction versions that use this model.
    /// </summary>
    public ICollection<AgentInstructionVersion> AgentInstructionVersions { get; set; } = new List<AgentInstructionVersion>();

    /// <summary>
    /// Navigation property for evaluation metrics performed using this model.
    /// </summary>
    public ICollection<MessageEvaluationMetric> EvaluationMetrics { get; set; } = new List<MessageEvaluationMetric>();
}
