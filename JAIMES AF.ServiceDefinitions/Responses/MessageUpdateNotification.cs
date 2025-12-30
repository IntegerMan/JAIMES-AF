namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Notification sent when a message has been processed by a worker.
/// Used for real-time SignalR updates to web clients.
/// </summary>
public record MessageUpdateNotification
{
    /// <summary>
    /// The ID of the message that was updated.
    /// </summary>
    public required int MessageId { get; init; }
    
    /// <summary>
    /// The ID of the game the message belongs to.
    /// </summary>
    public required Guid GameId { get; init; }
    
    /// <summary>
    /// The type of update that occurred.
    /// </summary>
    public required MessageUpdateType UpdateType { get; init; }
    
    /// <summary>
    /// Optional sentiment value for user messages (-1, 0, or 1).
    /// </summary>
    public int? Sentiment { get; init; }
    
    /// <summary>
    /// Optional evaluation metrics for assistant messages.
    /// </summary>
    public List<MessageEvaluationMetricResponse>? Metrics { get; init; }
}

/// <summary>
/// Type of message update notification.
/// </summary>
public enum MessageUpdateType
{
    /// <summary>
    /// Sentiment analysis completed for a user message.
    /// </summary>
    SentimentAnalyzed,
    
    /// <summary>
    /// Evaluation metrics calculated for an assistant message.
    /// </summary>
    MetricsEvaluated
}
