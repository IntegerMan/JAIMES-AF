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
    /// Optional sentiment confidence score (0.0 to 1.0) for user messages.
    /// </summary>
    public double? SentimentConfidence { get; init; }

    /// <summary>
    /// Optional sentiment source (0 = Model, 1 = Player) for user messages.
    /// </summary>
    public int? SentimentSource { get; init; }

    /// <summary>
    /// Optional evaluation metrics for assistant messages.
    /// </summary>
    public List<MessageEvaluationMetricResponse>? Metrics { get; init; }

    /// <summary>
    /// Optional message text for content-based matching.
    /// Included to enable correct ID assignment when notifications arrive out-of-order.
    /// </summary>
    public string? MessageText { get; init; }

    /// <summary>
    /// Optional flag indicating if the message is missing evaluators.
    /// </summary>
    public bool? HasMissingEvaluators { get; init; }

    /// <summary>
    /// Optional flag indicating if the message has tool calls.
    /// </summary>
    public bool? HasToolCalls { get; init; }
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
    MetricsEvaluated,

    /// <summary>
    /// Tool calls processed for an assistant message.
    /// </summary>
    ToolCallsProcessed
}
