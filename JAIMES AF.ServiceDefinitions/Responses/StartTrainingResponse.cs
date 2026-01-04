namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Response for the start training endpoint.
/// </summary>
public record StartTrainingResponse(
    int TrainingJobId,
    string Status);
