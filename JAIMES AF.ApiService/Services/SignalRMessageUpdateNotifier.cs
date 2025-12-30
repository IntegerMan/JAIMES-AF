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
    public async Task NotifySentimentAnalyzedAsync(int messageId, Guid gameId, int sentiment, double? confidence,
        CancellationToken cancellationToken = default)
    {
        MessageUpdateNotification notification = new()
        {
            MessageId = messageId,
            GameId = gameId,
            UpdateType = MessageUpdateType.SentimentAnalyzed,
            Sentiment = sentiment,
            SentimentConfidence = confidence
        };

        await BroadcastUpdateAsync(notification);
    }

    public async Task NotifyMetricsEvaluatedAsync(int messageId, Guid gameId,
        List<MessageEvaluationMetricResponse> metrics, CancellationToken cancellationToken = default)
    {
        MessageUpdateNotification notification = new()
        {
            MessageId = messageId,
            GameId = gameId,
            UpdateType = MessageUpdateType.MetricsEvaluated,
            Metrics = metrics
        };

        await BroadcastUpdateAsync(notification);
    }

    private async Task BroadcastUpdateAsync(MessageUpdateNotification notification)
    {
        string groupName = MessageHub.GetGameGroupName(notification.GameId);

        logger.LogDebug(
            "Broadcasting {UpdateType} update for message {MessageId} to game group {GameId}",
            notification.UpdateType,
            notification.MessageId,
            notification.GameId);

        await hubContext.Clients.Group(groupName).MessageUpdated(notification);
    }
}
