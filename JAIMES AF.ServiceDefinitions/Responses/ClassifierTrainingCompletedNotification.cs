namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// SignalR notification for classifier training completion.
/// </summary>
public record ClassifierTrainingCompletedNotification(
    int TrainingJobId,
    int? ClassificationModelId,
    bool Success,
    string? ErrorMessage);
