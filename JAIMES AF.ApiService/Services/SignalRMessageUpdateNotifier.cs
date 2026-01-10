using MattEland.Jaimes.ApiService.Hubs;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.AspNetCore.SignalR;

namespace MattEland.Jaimes.ApiService.Services;

/// <summary>
/// SignalR-based implementation of message update notifier for use within the API service.
/// Broadcasts updates directly to connected web clients via SignalR HubContext.
/// </summary>
public class SignalRMessageUpdateNotifier(
    IHubContext<MessageHub, IMessageHubClient> hubContext,
    ILogger<SignalRMessageUpdateNotifier> logger) : IMessageUpdateNotifier
{
    public async Task NotifyEarlySentimentAsync(
        Guid trackingGuid,
        Guid gameId,
        int sentiment,
        double confidence,
        CancellationToken cancellationToken = default)
    {
        MessageUpdateNotification notification = new()
        {
            MessageId = null,
            TrackingGuid = trackingGuid,
            GameId = gameId,
            UpdateType = MessageUpdateType.SentimentAnalyzed,
            Sentiment = sentiment,
            SentimentConfidence = confidence,
            SentimentSource = 0 // Model
        };

        logger.LogDebug(
            "Broadcasting early sentiment ({Sentiment}, confidence: {Confidence:P0}) for tracking GUID {TrackingGuid}",
            sentiment,
            confidence,
            trackingGuid);

        await BroadcastUpdateAsync(notification);
    }

    public async Task NotifySentimentAnalyzedAsync(int messageId,
        Guid gameId,
        int sentiment,
        double? confidence,
        string messageText,
        CancellationToken cancellationToken = default)
    {
        MessageUpdateNotification notification = new()
        {
            MessageId = messageId,
            GameId = gameId,
            UpdateType = MessageUpdateType.SentimentAnalyzed,
            Sentiment = sentiment,
            SentimentConfidence = confidence,
            SentimentSource = 0, // Model
            MessageText = messageText
        };

        await BroadcastUpdateAsync(notification);
    }

    public async Task NotifyMetricsEvaluatedAsync(int messageId,
        Guid gameId,
        List<MessageEvaluationMetricResponse> metrics,
        string messageText,
        bool hasMissingEvaluators,
        CancellationToken cancellationToken = default)
    {
        MessageUpdateNotification notification = new()
        {
            MessageId = messageId,
            GameId = gameId,
            UpdateType = MessageUpdateType.MetricsEvaluated,
            Metrics = metrics,
            MessageText = messageText,
            HasMissingEvaluators = hasMissingEvaluators
        };

        await BroadcastUpdateAsync(notification);
    }

    public async Task NotifyMetricEvaluatedAsync(int messageId,
        Guid gameId,
        MessageEvaluationMetricResponse metric,
        int expectedMetricCount,
        int completedMetricCount,
        bool isError = false,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        MessageUpdateNotification notification = new()
        {
            MessageId = messageId,
            GameId = gameId,
            UpdateType = MessageUpdateType.MetricEvaluated,
            Metrics = [metric],
            ExpectedMetricCount = expectedMetricCount,
            CompletedMetricCount = completedMetricCount,
            IsError = isError,
            ErrorMessage = errorMessage
        };

        await BroadcastUpdateAsync(notification);
    }

    public async Task NotifyToolCallsProcessedAsync(int messageId,
        Guid gameId,
        bool hasToolCalls,
        string? messageText = null,
        CancellationToken cancellationToken = default)
    {
        MessageUpdateNotification notification = new()
        {
            MessageId = messageId,
            GameId = gameId,
            UpdateType = MessageUpdateType.ToolCallsProcessed,
            HasToolCalls = hasToolCalls,
            MessageText = messageText
        };

        await BroadcastUpdateAsync(notification);
    }

    /// <summary>
    /// Stage-level notifications are not used in the API service context - they are for worker-to-hub communication.
    /// These no-op implementations satisfy the interface for DI compatibility.
    /// </summary>
    public Task NotifyStageStartedAsync(int messageId, Guid gameId, MessagePipelineType pipelineType,
        MessagePipelineStage stage, string? messagePreview = null, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task NotifyStageCompletedAsync(int messageId, Guid gameId, MessagePipelineType pipelineType,
        MessagePipelineStage stage, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task NotifyStageFailedAsync(int messageId, Guid gameId, MessagePipelineType pipelineType,
        MessagePipelineStage stage, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task NotifyEvaluatorStartedAsync(int messageId, Guid gameId, string evaluatorName,
        int evaluatorIndex, int totalEvaluators, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task NotifyEvaluatorCompletedAsync(int messageId, Guid gameId, string evaluatorName,
        int evaluatorIndex, int totalEvaluators, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    private async Task BroadcastUpdateAsync(MessageUpdateNotification notification)
    {
        string groupName = MessageHub.GetGameGroupName(notification.GameId);

        logger.LogDebug(
            "Broadcasting {UpdateType} update for message {MessageId} to game group {GameId}",
            notification.UpdateType,
            notification.MessageId,
            notification.GameId);

        await hubContext.Clients.Group(groupName).MessageUpdated(notification);
        await hubContext.Clients.Group("admin").MessageUpdated(notification);
    }

    public async Task NotifyClassifierTrainingCompletedAsync(
        ClassifierTrainingCompletedNotification notification,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug(
            "Broadcasting classifier training completed for job {JobId}, success: {Success}",
            notification.TrainingJobId,
            notification.Success);

        await hubContext.Clients.Group("admin").ClassifierTrainingCompleted(notification);
    }

    public async Task NotifyClassifierTrainingStatusChangedAsync(
        int trainingJobId,
        string status,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug(
            "Broadcasting classifier training status changed for job {JobId}, status: {Status}",
            trainingJobId,
            status);

        await hubContext.Clients.Group("admin").ClassifierTrainingStatusChanged(trainingJobId, status);
    }
}
