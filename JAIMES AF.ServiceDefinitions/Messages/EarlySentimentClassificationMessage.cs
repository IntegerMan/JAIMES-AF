namespace MattEland.Jaimes.ServiceDefinitions.Messages;

/// <summary>
/// Message queued for early sentiment classification of a user message before it's persisted to the database.
/// Uses a tracking GUID instead of a message ID for correlation.
/// </summary>
public class EarlySentimentClassificationMessage
{
    /// <summary>
    /// Tracking GUID for correlating SignalR notifications with the client request.
    /// </summary>
    public required Guid TrackingGuid { get; set; }

    /// <summary>
    /// The game ID for SignalR notifications.
    /// </summary>
    public required Guid GameId { get; set; }

    /// <summary>
    /// The message text to classify.
    /// </summary>
    public required string MessageText { get; set; }
}
