namespace MattEland.Jaimes.ServiceDefinitions.Messages;

/// <summary>
/// Message indicating that classifier training has completed (successfully or with an error).
/// </summary>
public class ClassifierTrainedMessage
{
    /// <summary>
    /// ID of the training job that completed.
    /// </summary>
    public int TrainingJobId { get; set; }

    /// <summary>
    /// ID of the resulting classification model (if successful).
    /// </summary>
    public int? ClassificationModelId { get; set; }

    /// <summary>
    /// Whether training completed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if training failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
