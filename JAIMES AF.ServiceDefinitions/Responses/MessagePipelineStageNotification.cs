namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Notification sent when a message processing stage changes.
/// Used for real-time SignalR updates to web clients to show pipeline progress.
/// </summary>
public record MessagePipelineStageNotification
{
    /// <summary>
    /// The ID of the message being processed.
    /// </summary>
    public required int MessageId { get; init; }

    /// <summary>
    /// The ID of the game the message belongs to.
    /// </summary>
    public required Guid GameId { get; init; }

    /// <summary>
    /// The type of pipeline (User or Assistant).
    /// </summary>
    public required MessagePipelineType PipelineType { get; init; }

    /// <summary>
    /// The current stage of processing.
    /// </summary>
    public required MessagePipelineStage Stage { get; init; }

    /// <summary>
    /// The status of the current stage (Started, Completed, Failed).
    /// </summary>
    public required MessagePipelineStageStatus StageStatus { get; init; }

    /// <summary>
    /// Optional name of the evaluator currently running (for assistant pipeline).
    /// </summary>
    public string? EvaluatorName { get; init; }

    /// <summary>
    /// Current evaluator index (1-based) when running evaluators.
    /// </summary>
    public int? EvaluatorIndex { get; init; }

    /// <summary>
    /// Total number of evaluators to run.
    /// </summary>
    public int? TotalEvaluators { get; init; }

    /// <summary>
    /// When this stage started (for timeout detection).
    /// </summary>
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Timestamp when this notification was created.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Optional source identifier for the worker reporting this status.
    /// </summary>
    public string? WorkerSource { get; init; }

    /// <summary>
    /// Optional preview of the message text (first 100 chars).
    /// </summary>
    public string? MessagePreview { get; init; }
}

/// <summary>
/// Type of message processing pipeline.
/// </summary>
public enum MessagePipelineType
{
    /// <summary>
    /// User message pipeline (sentiment analysis).
    /// </summary>
    User,

    /// <summary>
    /// Assistant message pipeline (evaluation metrics).
    /// </summary>
    Assistant
}

/// <summary>
/// Stages in the message processing pipeline.
/// </summary>
public enum MessagePipelineStage
{
    /// <summary>
    /// Message is queued and waiting to be processed.
    /// </summary>
    Queued,

    /// <summary>
    /// Loading message from database.
    /// </summary>
    Loading,

    /// <summary>
    /// Running sentiment analysis (user messages only).
    /// </summary>
    SentimentAnalysis,

    /// <summary>
    /// Running evaluators (assistant messages only).
    /// </summary>
    Evaluation,

    /// <summary>
    /// Queuing message for embedding.
    /// </summary>
    EmbeddingQueue,

    /// <summary>
    /// Processing complete.
    /// </summary>
    Complete,

    /// <summary>
    /// Processing failed.
    /// </summary>
    Failed
}

/// <summary>
/// Status of a pipeline stage.
/// </summary>
public enum MessagePipelineStageStatus
{
    /// <summary>
    /// Stage has started.
    /// </summary>
    Started,

    /// <summary>
    /// Stage has completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Stage has failed.
    /// </summary>
    Failed
}
