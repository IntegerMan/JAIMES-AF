using System.Diagnostics;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.Workers.UserMessageWorker.Consumers;

/// <summary>
/// Consumes early sentiment classification messages for user messages before persistence.
/// Classifies sentiment and broadcasts result via SignalR using tracking GUID.
/// </summary>
public class EarlySentimentClassificationConsumer(
    ISentimentClassificationService sentimentService,
    IMessageUpdateNotifier messageUpdateNotifier,
    ILogger<EarlySentimentClassificationConsumer> logger,
    ActivitySource activitySource) : IMessageConsumer<EarlySentimentClassificationMessage>
{
    public async Task HandleAsync(
        EarlySentimentClassificationMessage message,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = activitySource.StartActivity("EarlySentimentClassification.Process");
        activity?.SetTag("messaging.message_type", nameof(EarlySentimentClassificationMessage));
        activity?.SetTag("tracking.guid", message.TrackingGuid);
        activity?.SetTag("game.id", message.GameId);

        logger.LogInformation(
            "Processing early sentiment classification for tracking GUID {TrackingGuid}",
            message.TrackingGuid);

        try
        {
            // Classify sentiment
            var (sentiment, confidence) = await sentimentService.ClassifyAsync(
                message.MessageText,
                cancellationToken);

            logger.LogInformation(
                "Early sentiment classified: {Sentiment} (confidence: {Confidence:P0}) for tracking GUID {TrackingGuid}",
                sentiment,
                confidence,
                message.TrackingGuid);

            // Broadcast result via SignalR
            await messageUpdateNotifier.NotifyEarlySentimentAsync(
                message.TrackingGuid,
                message.GameId,
                sentiment,
                confidence,
                cancellationToken);

            logger.LogDebug(
                "Broadcasted early sentiment for tracking GUID {TrackingGuid}",
                message.TrackingGuid);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to classify early sentiment for tracking GUID {TrackingGuid}",
                message.TrackingGuid);
            throw; // Let consumer service handle retry/error queue
        }
    }
}
