using System.ComponentModel.DataAnnotations;

namespace MattEland.Jaimes.Repositories.Entities;

/// <summary>
/// Tracks training job parameters and status for custom classifier training.
/// </summary>
public class ClassificationModelTrainingJob
{
    public int Id { get; set; }

    /// <summary>
    /// Status of the training job: Queued, Training, Completed, Failed.
    /// </summary>
    [MaxLength(50)]
    public required string Status { get; set; }

    /// <summary>
    /// Minimum confidence threshold for including messages in training data.
    /// </summary>
    public double MinConfidence { get; set; }

    /// <summary>
    /// Percentage of data to use for training (vs. testing).
    /// </summary>
    public double TrainTestSplit { get; set; }

    /// <summary>
    /// Maximum training time in seconds.
    /// </summary>
    public int TrainingTimeSeconds { get; set; }

    /// <summary>
    /// The AutoML metric to optimize (e.g., MacroAccuracy, MicroAccuracy, LogLoss).
    /// </summary>
    [MaxLength(50)]
    public required string OptimizingMetric { get; set; }

    /// <summary>
    /// Total rows matching the confidence filter.
    /// </summary>
    public int? TotalRows { get; set; }

    /// <summary>
    /// Number of rows used for training.
    /// </summary>
    public int? TrainingRows { get; set; }

    /// <summary>
    /// Number of rows used for testing.
    /// </summary>
    public int? TestRows { get; set; }

    // Evaluation metrics from AutoML training
    public double? MacroAccuracy { get; set; }
    public double? MicroAccuracy { get; set; }
    public double? MacroPrecision { get; set; }
    public double? MacroRecall { get; set; }
    public double? LogLoss { get; set; }

    /// <summary>
    /// JSON representation of the confusion matrix.
    /// </summary>
    public string? ConfusionMatrixJson { get; set; }

    /// <summary>
    /// Name of the best ML.NET trainer selected by AutoML.
    /// </summary>
    [MaxLength(200)]
    public string? TrainerName { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Error message if training failed.
    /// </summary>
    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Reference to the trained model (set after successful training).
    /// </summary>
    public int? ClassificationModelId { get; set; }

    /// <summary>
    /// Navigation property to the trained model.
    /// </summary>
    public ClassificationModel? ClassificationModel { get; set; }
}
