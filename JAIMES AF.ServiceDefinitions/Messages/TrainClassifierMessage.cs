namespace MattEland.Jaimes.ServiceDefinitions.Messages;

/// <summary>
/// Message to request training a new classifier from user messages.
/// </summary>
public class TrainClassifierMessage
{
    /// <summary>
    /// ID of the training job in the database.
    /// </summary>
    public int TrainingJobId { get; set; }

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
    public required string OptimizingMetric { get; set; }
}
