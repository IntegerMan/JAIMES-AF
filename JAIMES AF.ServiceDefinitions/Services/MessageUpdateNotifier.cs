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

    public async Task NotifySentimentAnalyzedAsync(int messageId, Guid gameId, int sentiment, double? confidence,
        string messageText, CancellationToken cancellationToken = default)
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

    public async Task NotifyMetricsEvaluatedAsync(int messageId, Guid gameId,
        List<MessageEvaluationMetricResponse> metrics, string messageText,
        bool hasMissingEvaluators, CancellationToken cancellationToken = default)
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

    public async Task NotifyMetricEvaluatedAsync(int messageId, Guid gameId,
        MessageEvaluationMetricResponse metric, int expectedMetricCount, int completedMetricCount,
        bool isError = false, string? errorMessage = null, CancellationToken cancellationToken = default)
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

    public async Task NotifyToolCallsProcessedAsync(int messageId, Guid gameId, bool hasToolCalls,
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
            _logger.LogWarning(ex, "Failed to send message update notification for message {MessageId}",
                notification.MessageId);
        }
    }
}
