namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Notification sent when a message has been processed by a worker.
/// Used for real-time SignalR updates to web clients.
/// </summary>
public record MessageUpdateNotification
{
    /// <summary>
    /// The ID of the message that was updated.
    /// Null for early sentiment classification (use TrackingGuid instead).
    /// </summary>
    public int? MessageId { get; init; }

    /// <summary>
    /// Tracking GUID for early sentiment classification (before message persistence).
    /// Null for regular message updates (use MessageId instead).
    /// </summary>
    public Guid? TrackingGuid { get; init; }

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

    /// <summary>
    /// The total number of expected metrics for this message.
    /// Used for progressive UI updates during streaming evaluation.
    /// </summary>
    public int? ExpectedMetricCount { get; init; }

    /// <summary>
    /// The number of metrics that have been evaluated so far.
    /// Used for progressive UI updates during streaming evaluation.
    /// </summary>
    public int? CompletedMetricCount { get; init; }

    /// <summary>
    /// Optional flag indicating if the metric evaluation resulted in an error.
    /// </summary>
    public bool? IsError { get; init; }

    /// <summary>
    /// Optional error message if the evaluation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
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
    /// This is the final notification sent when all evaluators have completed.
    /// </summary>
    MetricsEvaluated,

    /// <summary>
    /// A single metric has been evaluated (streaming update).
    /// Sent as each evaluator completes, before the final MetricsEvaluated.
    /// </summary>
    MetricEvaluated,

    /// <summary>
    /// Tool calls processed for an assistant message.
    /// </summary>
    ToolCallsProcessed,

    /// <summary>
    /// A pipeline processing stage has started.
    /// </summary>
    PipelineStageStarted,

    /// <summary>
    /// A pipeline processing stage has completed.
    /// </summary>
    PipelineStageCompleted,

    /// <summary>
    /// An evaluator has started processing.
    /// </summary>
    EvaluatorStarted,

    /// <summary>
    /// An evaluator has completed processing.
    /// </summary>
    EvaluatorCompleted
}
