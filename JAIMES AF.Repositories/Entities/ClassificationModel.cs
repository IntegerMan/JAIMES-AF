using System.ComponentModel.DataAnnotations;

namespace MattEland.Jaimes.Repositories.Entities;

/// <summary>
/// Entity for tracking classification models stored in the database.
/// References a StoredFile for the actual binary model content.
/// </summary>
public class ClassificationModel
{
    public int Id { get; set; }

    /// <summary>
    /// Type of classification model (e.g., "SentimentClassification").
    /// </summary>
    [MaxLength(100)]
    public required string ModelType { get; set; }

    /// <summary>
    /// Descriptive name for the model.
    /// </summary>
    [MaxLength(200)]
    public required string Name { get; set; }

    /// <summary>
    /// Optional description of the model.
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Reference to the stored file containing the model binary.
    /// </summary>
    public int StoredFileId { get; set; }

    /// <summary>
    /// Navigation property to the stored file.
    /// </summary>
    public StoredFile StoredFile { get; set; } = null!;

    /// <summary>
    /// When the model was uploaded to the database.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Whether this model is the active model used for classification.
    /// Only one model per ModelType should be active at a time.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Reference to the training job that created this model (null for externally trained models).
    /// </summary>
    public int? TrainingJobId { get; set; }

    /// <summary>
    /// Navigation property to the training job.
    /// </summary>
    public ClassificationModelTrainingJob? TrainingJob { get; set; }
}
