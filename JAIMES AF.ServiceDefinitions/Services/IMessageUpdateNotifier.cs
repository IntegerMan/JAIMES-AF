using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.ServiceDefinitions.Services;

/// <summary>
/// Service for notifying the API about message updates.
/// Workers use this to trigger real-time SignalR broadcasts to web clients.
/// </summary>
public interface IMessageUpdateNotifier
{
    /// <summary>
    /// Notifies the API that a user message has been analyzed for sentiment.
    /// </summary>
    /// <param name="messageId">The message ID.</param>
    /// <param name="gameId">The game ID.</param>
    /// <param name="sentiment">The sentiment value (-1, 0, or 1).</param>
    /// <param name="confidence">The sentiment confidence score (0.0 to 1.0).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifySentimentAnalyzedAsync(int messageId, Guid gameId, int sentiment, double? confidence,
        string messageText, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies the API that an assistant message has been evaluated for metrics.
    /// </summary>
    /// <param name="messageId">The message ID.</param>
    /// <param name="gameId">The game ID.</param>
    /// <param name="metrics">The evaluation metrics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyMetricsEvaluatedAsync(int messageId, Guid gameId, List<MessageEvaluationMetricResponse> metrics,
        string messageText, bool hasMissingEvaluators, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies clients that tool calls have been processed for a message.
    /// </summary>
    Task NotifyToolCallsProcessedAsync(
        int messageId,
        Guid gameId,
        bool hasToolCalls,
        string? messageText = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies clients that a pipeline stage has started for a message.
    /// </summary>
    /// <param name="messageId">The message ID.</param>
    /// <param name="gameId">The game ID.</param>
    /// <param name="pipelineType">The type of pipeline (User or Assistant).</param>
    /// <param name="stage">The stage that started.</param>
    /// <param name="messagePreview">Optional preview of the message text.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyStageStartedAsync(
        int messageId,
        Guid gameId,
        MessagePipelineType pipelineType,
        MessagePipelineStage stage,
        string? messagePreview = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies clients that a pipeline stage has completed for a message.
    /// </summary>
    /// <param name="messageId">The message ID.</param>
    /// <param name="gameId">The game ID.</param>
    /// <param name="pipelineType">The type of pipeline (User or Assistant).</param>
    /// <param name="stage">The stage that completed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyStageCompletedAsync(
        int messageId,
        Guid gameId,
        MessagePipelineType pipelineType,
        MessagePipelineStage stage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies clients that a pipeline stage has failed for a message.
    /// </summary>
    /// <param name="messageId">The message ID.</param>
    /// <param name="gameId">The game ID.</param>
    /// <param name="pipelineType">The type of pipeline (User or Assistant).</param>
    /// <param name="stage">The stage that failed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyStageFailedAsync(
        int messageId,
        Guid gameId,
        MessagePipelineType pipelineType,
        MessagePipelineStage stage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies clients that an evaluator has started processing a message.
    /// </summary>
    /// <param name="messageId">The message ID.</param>
    /// <param name="gameId">The game ID.</param>
    /// <param name="evaluatorName">The name of the evaluator.</param>
    /// <param name="evaluatorIndex">The 1-based index of the evaluator.</param>
    /// <param name="totalEvaluators">The total number of evaluators to run.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyEvaluatorStartedAsync(
        int messageId,
        Guid gameId,
        string evaluatorName,
        int evaluatorIndex,
        int totalEvaluators,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies clients that an evaluator has completed processing a message.
    /// </summary>
    /// <param name="messageId">The message ID.</param>
    /// <param name="gameId">The game ID.</param>
    /// <param name="evaluatorName">The name of the evaluator.</param>
    /// <param name="evaluatorIndex">The 1-based index of the evaluator.</param>
    /// <param name="totalEvaluators">The total number of evaluators to run.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyEvaluatorCompletedAsync(
        int messageId,
        Guid gameId,
        string evaluatorName,
        int evaluatorIndex,
        int totalEvaluators,
        CancellationToken cancellationToken = default);
}
