using System.Net.Http.Json;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using Microsoft.Extensions.Logging;

namespace MattEland.Jaimes.ServiceDefinitions.Services;

/// <summary>
/// HTTP-based implementation of message update notifier.
/// Calls the API's internal endpoint to trigger SignalR broadcasts.
/// </summary>
public class MessageUpdateNotifier : IMessageUpdateNotifier
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MessageUpdateNotifier> _logger;

    public MessageUpdateNotifier(HttpClient httpClient, ILogger<MessageUpdateNotifier> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
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
            SentimentSource = 0, // Model source for worker-generated sentiment
            MessageText = messageText
        };

        await SendNotificationAsync(notification, cancellationToken);
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

        await SendNotificationAsync(notification, cancellationToken);
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

        await SendNotificationAsync(notification, cancellationToken);
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

        await SendNotificationAsync(notification, cancellationToken);
    }

    public async Task NotifyStageStartedAsync(
        int messageId,
        Guid gameId,
        MessagePipelineType pipelineType,
        MessagePipelineStage stage,
        string? messagePreview = null,
        CancellationToken cancellationToken = default)
    {
        MessagePipelineStageNotification notification = new()
        {
            MessageId = messageId,
            GameId = gameId,
            PipelineType = pipelineType,
            Stage = stage,
            StageStatus = MessagePipelineStageStatus.Started,
            MessagePreview = messagePreview?.Length > 100 ? messagePreview[..100] + "..." : messagePreview,
            WorkerSource = Environment.MachineName
        };

        await SendPipelineNotificationAsync(notification, cancellationToken);
    }

    public async Task NotifyStageCompletedAsync(
        int messageId,
        Guid gameId,
        MessagePipelineType pipelineType,
        MessagePipelineStage stage,
        CancellationToken cancellationToken = default)
    {
        MessagePipelineStageNotification notification = new()
        {
            MessageId = messageId,
            GameId = gameId,
            PipelineType = pipelineType,
            Stage = stage,
            StageStatus = MessagePipelineStageStatus.Completed,
            WorkerSource = Environment.MachineName
        };

        await SendPipelineNotificationAsync(notification, cancellationToken);
    }

    public async Task NotifyStageFailedAsync(
        int messageId,
        Guid gameId,
        MessagePipelineType pipelineType,
        MessagePipelineStage stage,
        CancellationToken cancellationToken = default)
    {
        MessagePipelineStageNotification notification = new()
        {
            MessageId = messageId,
            GameId = gameId,
            PipelineType = pipelineType,
            Stage = stage,
            StageStatus = MessagePipelineStageStatus.Failed,
            WorkerSource = Environment.MachineName
        };

        await SendPipelineNotificationAsync(notification, cancellationToken);
    }

    public async Task NotifyEvaluatorStartedAsync(
        int messageId,
        Guid gameId,
        string evaluatorName,
        int evaluatorIndex,
        int totalEvaluators,
        CancellationToken cancellationToken = default)
    {
        MessagePipelineStageNotification notification = new()
        {
            MessageId = messageId,
            GameId = gameId,
            PipelineType = MessagePipelineType.Assistant,
            Stage = MessagePipelineStage.Evaluation,
            StageStatus = MessagePipelineStageStatus.Started,
            EvaluatorName = evaluatorName,
            EvaluatorIndex = evaluatorIndex,
            TotalEvaluators = totalEvaluators,
            WorkerSource = Environment.MachineName
        };

        await SendPipelineNotificationAsync(notification, cancellationToken);
    }

    public async Task NotifyEvaluatorCompletedAsync(
        int messageId,
        Guid gameId,
        string evaluatorName,
        int evaluatorIndex,
        int totalEvaluators,
        CancellationToken cancellationToken = default)
    {
        MessagePipelineStageNotification notification = new()
        {
            MessageId = messageId,
            GameId = gameId,
            PipelineType = MessagePipelineType.Assistant,
            Stage = MessagePipelineStage.Evaluation,
            StageStatus = MessagePipelineStageStatus.Completed,
            EvaluatorName = evaluatorName,
            EvaluatorIndex = evaluatorIndex,
            TotalEvaluators = totalEvaluators,
            WorkerSource = Environment.MachineName
        };

        await SendPipelineNotificationAsync(notification, cancellationToken);
    }

    private async Task SendNotificationAsync(MessageUpdateNotification notification,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug(
                "Sending {UpdateType} notification for message {MessageId} in game {GameId}",
                notification.UpdateType,
                notification.MessageId,
                notification.GameId);

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                "/internal/message-updates",
                notification,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to send message update notification: {StatusCode}",
                    response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            // Log but don't throw - notification failure shouldn't fail message processing
            _logger.LogWarning(ex,
                "Failed to send message update notification for message {MessageId}",
                notification.MessageId);
        }
    }

    private async Task SendPipelineNotificationAsync(MessagePipelineStageNotification notification,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug(
                "Sending pipeline stage {Stage} ({StageStatus}) notification for message {MessageId} in game {GameId}",
                notification.Stage,
                notification.StageStatus,
                notification.MessageId,
                notification.GameId);

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                "/internal/message-pipeline-updates",
                notification,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to send message pipeline notification: {StatusCode}",
                    response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            // Log but don't throw - notification failure shouldn't fail message processing
            _logger.LogWarning(ex, "Failed to send message pipeline notification for message {MessageId}",
                notification.MessageId);
        }
    }

    public async Task NotifyClassifierTrainingCompletedAsync(
        ClassifierTrainingCompletedNotification notification,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug(
                "Sending classifier training completed notification for job {JobId}, success: {Success}",
                notification.TrainingJobId,
                notification.Success);

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                "/internal/classifier-training-completed",
                notification,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to send classifier training notification: {StatusCode}",
                    response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to send classifier training notification for job {JobId}",
                notification.TrainingJobId);
        }
    }

    public async Task NotifyClassifierTrainingStatusChangedAsync(
        int trainingJobId,
        string status,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug(
                "Sending classifier training status changed notification for job {JobId}, status: {Status}",
                trainingJobId,
                status);

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                "/internal/classifier-training-status",
                new {TrainingJobId = trainingJobId, Status = status},
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to send classifier training status notification: {StatusCode}",
                    response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to send classifier training status notification for job {JobId}",
                trainingJobId);
        }
    }
}
