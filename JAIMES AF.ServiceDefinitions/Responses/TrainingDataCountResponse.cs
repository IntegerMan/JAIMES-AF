namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Response DTO for training data count based on confidence filter.
/// </summary>
public record TrainingDataCountResponse(
    int TotalMessagesWithSentiment,
    int MessagesAboveConfidence,
    double MinConfidence);
