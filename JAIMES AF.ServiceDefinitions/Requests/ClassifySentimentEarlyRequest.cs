namespace MattEland.Jaimes.ServiceDefinitions.Requests;

/// <summary>
/// Request to classify sentiment for a user message before it's persisted to the database.
/// </summary>
public class ClassifySentimentEarlyRequest
{
    /// <summary>
    /// The game ID for SignalR notifications.
    /// </summary>
    public required Guid GameId { get; set; }

    /// <summary>
    /// The message text to classify.
    /// </summary>
    public required string MessageText { get; set; }

    /// <summary>
    /// Optional tracking GUID provided by the client for correlation.
    /// If not provided, the server will generate one.
    /// </summary>
    public Guid? TrackingGuid { get; set; }
}
