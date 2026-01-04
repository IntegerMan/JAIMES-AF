namespace MattEland.Jaimes.ServiceDefinitions.Requests;

/// <summary>
/// Request to start training a new classifier from user messages.
/// </summary>
/// <param name="MinConfidence">Minimum confidence threshold (0.0 - 1.0) for including messages.</param>
/// <param name="TrainTestSplit">Percentage of data used for training (0.6 - 0.9).</param>
/// <param name="TrainingTimeSeconds">Maximum training time in seconds (5, 30, 60, 120, 300, 600).</param>
/// <param name="OptimizingMetric">AutoML metric to optimize (MacroAccuracy, MicroAccuracy, LogLoss, TopKAccuracy).</param>
public record TrainClassifierRequest(
    double MinConfidence,
    double TrainTestSplit,
    int TrainingTimeSeconds,
    string OptimizingMetric);
