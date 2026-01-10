using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.Workers.UserMessageWorker.Options;
using MattEland.Jaimes.Workers.UserMessageWorker.Services;
using Microsoft.Extensions.Options;

namespace MattEland.Jaimes.Workers.UserMessageWorker.Consumers;

public class UserMessageConsumer(
    IDbContextFactory<JaimesDbContext> contextFactory,
    IMessagePublisher messagePublisher,
    ILogger<UserMessageConsumer> logger,
    ActivitySource activitySource,
    IOptions<SentimentAnalysisOptions> sentimentOptions,
    SentimentModelService sentimentModelService,
    IPendingSentimentCache pendingSentimentCache,
    IMessageUpdateNotifier messageUpdateNotifier) : IMessageConsumer<ConversationMessageQueuedMessage>
{
    public async Task HandleAsync(ConversationMessageQueuedMessage message,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = activitySource.StartActivity("UserMessage.Process");
        activity?.SetTag("messaging.message_type", nameof(ConversationMessageQueuedMessage));
        activity?.SetTag("message.id", message.MessageId);
        activity?.SetTag("message.game_id", message.GameId.ToString());
        activity?.SetTag("message.role", message.Role.ToString());

        try
        {
            // Note: Role-based routing ensures only User messages reach this consumer
            // No need to filter by role here

            logger.LogInformation(
                "Processing user message: MessageId={MessageId}, GameId={GameId}",
                message.MessageId,
                message.GameId);

            // Notify pipeline stage: Loading
            await messageUpdateNotifier.NotifyStageStartedAsync(
                message.MessageId,
                message.GameId,
                MessagePipelineType.User,
                MessagePipelineStage.Loading,
                cancellationToken: cancellationToken);

            // Load message from database
            await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
            Message? messageEntity = await context.Messages
                .Include(m => m.Game)
                .Include(m => m.Player)
                .Include(m => m.MessageSentiment)
                .FirstOrDefaultAsync(m => m.Id == message.MessageId, cancellationToken);

            if (messageEntity == null)
            {
                logger.LogWarning(
                    "Message {MessageId} not found in database. It may have been deleted.",
                    message.MessageId);
                activity?.SetStatus(ActivityStatusCode.Error, "Message not found");

                await messageUpdateNotifier.NotifyStageFailedAsync(
                    message.MessageId,
                    message.GameId,
                    MessagePipelineType.User,
                    MessagePipelineStage.Loading,
                    cancellationToken);
                return;
            }

            // Notify pipeline stage: Loading completed
            await messageUpdateNotifier.NotifyStageCompletedAsync(
                message.MessageId,
                message.GameId,
                MessagePipelineType.User,
                MessagePipelineStage.Loading,
                cancellationToken);

            // Set additional trace tags
            activity?.SetTag("message.text_length", messageEntity.Text?.Length ?? 0);
            activity?.SetTag("message.text", messageEntity.Text ?? "(empty)");
            activity?.SetTag("message.created_at", messageEntity.CreatedAt.ToString("O"));
            if (messageEntity.PlayerId != null)
            {
                activity?.SetTag("message.player_id", messageEntity.PlayerId);
            }

            // Log message details
            string textPreview = messageEntity.Text?.Length > 200
                ? messageEntity.Text.Substring(0, 200) + "..."
                : messageEntity.Text ?? "(empty)";

            logger.LogInformation(
                "User message details - MessageId: {MessageId}, GameId: {GameId}, PlayerId: {PlayerId}, " +
                "TextLength: {TextLength}, CreatedAt: {CreatedAt}, TextPreview: {TextPreview}",
                messageEntity.Id,
                messageEntity.GameId,
                messageEntity.PlayerId ?? "(none)",
                messageEntity.Text?.Length ?? 0,
                messageEntity.CreatedAt,
                textPreview);

            // Notify pipeline stage: Embedding Queue
            await messageUpdateNotifier.NotifyStageStartedAsync(
                messageEntity.Id,
                messageEntity.GameId,
                MessagePipelineType.User,
                MessagePipelineStage.EmbeddingQueue,
                messageEntity.Text,
                cancellationToken);

            // Enqueue message for embedding
            ConversationMessageReadyForEmbeddingMessage embeddingMessage = new()
            {
                MessageId = messageEntity.Id,
                GameId = messageEntity.GameId,
                Text = messageEntity.Text ?? string.Empty,
                Role = ChatRole.User,
                CreatedAt = messageEntity.CreatedAt
            };
            await messagePublisher.PublishAsync(embeddingMessage, cancellationToken);
            logger.LogDebug("Enqueued user message {MessageId} for embedding", messageEntity.Id);

            // Notify pipeline stage: Embedding Queue completed
            await messageUpdateNotifier.NotifyStageCompletedAsync(
                messageEntity.Id,
                messageEntity.GameId,
                MessagePipelineType.User,
                MessagePipelineStage.EmbeddingQueue,
                cancellationToken);

            // Check if early sentiment classification result is available in our cache
            bool sentimentAnalysisSucceeded;

            if (message.TrackingGuid.HasValue &&
                pendingSentimentCache.TryGet(message.TrackingGuid.Value, out var cachedResult))
            {
                logger.LogInformation(
                    "Using cached early sentiment classification for message {MessageId} (TrackingGuid: {TrackingGuid})",
                    message.MessageId,
                    message.TrackingGuid.Value);

                // Update entity with cached results
                if (messageEntity.MessageSentiment == null)
                {
                    DateTime now = DateTime.UtcNow;
                    messageEntity.MessageSentiment = new MessageSentiment
                    {
                        MessageId = messageEntity.Id,
                        Sentiment = cachedResult!.Sentiment,
                        Confidence = cachedResult.Confidence,
                        CreatedAt = now,
                        UpdatedAt = now,
                        SentimentSource = SentimentSource.Model
                    };
                    context.MessageSentiments.Add(messageEntity.MessageSentiment);
                }
                else if (messageEntity.MessageSentiment.SentimentSource != SentimentSource.Player)
                {
                    messageEntity.MessageSentiment.Sentiment = cachedResult!.Sentiment;
                    messageEntity.MessageSentiment.Confidence = cachedResult.Confidence;
                    messageEntity.MessageSentiment.UpdatedAt = DateTime.UtcNow;
                }

                // Save the cached sentiment to database
                await context.SaveChangesAsync(cancellationToken);

                // Notify web clients via SignalR
                await messageUpdateNotifier.NotifySentimentAnalyzedAsync(
                    messageEntity.Id,
                    messageEntity.GameId,
                    cachedResult!.Sentiment,
                    cachedResult.Confidence,
                    messageEntity.Text ?? string.Empty,
                    cancellationToken);

                sentimentAnalysisSucceeded = true;

                // Clean up the cache entry now that we've used it
                pendingSentimentCache.Remove(message.TrackingGuid.Value);
            }
            else if (message.SkipSentimentAnalysis && messageEntity.MessageSentiment != null)
            {
                // Sentiment already set (maybe from a previous attempt or already persisted)
                logger.LogInformation(
                    "Skipping sentiment analysis for message {MessageId} (already has sentiment)",
                    message.MessageId);
                sentimentAnalysisSucceeded = true;
            }
            else
            {
                // No cached result available, run sentiment analysis
                sentimentAnalysisSucceeded =
                    await AnalyzeSentimentAsync(messageEntity, activity, context, cancellationToken);
            }

            // Notify pipeline stage: Complete or Failed
            if (sentimentAnalysisSucceeded)
            {
                await messageUpdateNotifier.NotifyStageCompletedAsync(
                    messageEntity.Id,
                    messageEntity.GameId,
                    MessagePipelineType.User,
                    MessagePipelineStage.Complete,
                    cancellationToken);

                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            else
            {
                await messageUpdateNotifier.NotifyStageFailedAsync(
                    messageEntity.Id,
                    messageEntity.GameId,
                    MessagePipelineType.User,
                    MessagePipelineStage.SentimentAnalysis,
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process user message {MessageId}", message.MessageId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            // Notify pipeline stage: Failed
            await messageUpdateNotifier.NotifyStageFailedAsync(
                message.MessageId,
                message.GameId,
                MessagePipelineType.User,
                MessagePipelineStage.Failed,
                cancellationToken);

            // Re-throw to let message consumer service handle retry logic
            throw;
        }
    }

    private async Task<bool> AnalyzeSentimentAsync(Message messageEntity,
        Activity? activity,
        JaimesDbContext context,
        CancellationToken cancellationToken)
    {
        // Check if sentiment already exists (early classification or manual override)
        if (messageEntity.MessageSentiment != null)
        {
            logger.LogInformation("Message {MessageId} already has sentiment (source: {Source}), skipping analysis",
                messageEntity.Id,
                messageEntity.MessageSentiment.SentimentSource);
            return true;
        }

        // Perform sentiment analysis
        if (!string.IsNullOrWhiteSpace(messageEntity.Text))
        {
            // Notify pipeline stage: Sentiment Analysis started
            await messageUpdateNotifier.NotifyStageStartedAsync(
                messageEntity.Id,
                messageEntity.GameId,
                MessagePipelineType.User,
                MessagePipelineStage.SentimentAnalysis,
                messageEntity.Text,
                cancellationToken);

            try
            {
                double confidenceThreshold = sentimentOptions.Value.ConfidenceThreshold;
                (int sentiment, double confidence) = sentimentModelService.AnalyzeSentimentWithThreshold(
                    messageEntity.Text,
                    confidenceThreshold);

                activity?.SetTag("sentiment.value", sentiment);
                activity?.SetTag("sentiment.confidence", confidence);
                activity?.SetTag("sentiment.threshold", confidenceThreshold);

                // Create or update MessageSentiment entity
                DateTime now = DateTime.UtcNow;
                MessageSentiment? existingSentiment = messageEntity.MessageSentiment;

                if (existingSentiment == null)
                {
                    // Create new sentiment record
                    MessageSentiment newSentiment = new()
                    {
                        MessageId = messageEntity.Id,
                        Sentiment = sentiment,
                        Confidence = confidence,
                        CreatedAt = now,
                        UpdatedAt = now,
                        SentimentSource = SentimentSource.Model
                    };
                    context.MessageSentiments.Add(newSentiment);
                }
                else
                {
                    // If the sentiment was set by a player, do not overwrite it
                    if (existingSentiment.SentimentSource == SentimentSource.Player)
                    {
                        logger.LogInformation(
                            "Skipping sentiment update for message {MessageId} as it was manually set by a player.",
                            messageEntity.Id);

                        // Notify pipeline stage: Sentiment Analysis completed (skipped)
                        await messageUpdateNotifier.NotifyStageCompletedAsync(
                            messageEntity.Id,
                            messageEntity.GameId,
                            MessagePipelineType.User,
                            MessagePipelineStage.SentimentAnalysis,
                            cancellationToken);

                        return true;
                    }

                    // Update existing sentiment record
                    existingSentiment.Sentiment = sentiment;
                    existingSentiment.Confidence = confidence;
                    existingSentiment.UpdatedAt = now;
                    existingSentiment.SentimentSource = SentimentSource.Model;
                }

                logger.LogInformation(
                    "Sentiment analysis completed - MessageId: {MessageId}, Sentiment: {Sentiment}, Confidence: {Confidence}, Threshold: {Threshold}",
                    messageEntity.Id,
                    sentiment,
                    confidence,
                    confidenceThreshold);

                // Save sentiment to database
                await context.SaveChangesAsync(cancellationToken);

                // Notify pipeline stage: Sentiment Analysis completed
                await messageUpdateNotifier.NotifyStageCompletedAsync(
                    messageEntity.Id,
                    messageEntity.GameId,
                    MessagePipelineType.User,
                    MessagePipelineStage.SentimentAnalysis,
                    cancellationToken);

                // Notify web clients via SignalR
                await messageUpdateNotifier.NotifySentimentAnalyzedAsync(
                    messageEntity.Id,
                    messageEntity.GameId,
                    sentiment,
                    confidence,
                    messageEntity.Text ?? string.Empty,
                    cancellationToken);

                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to perform sentiment analysis for message {MessageId}", messageEntity.Id);
                activity?.SetTag("sentiment.error", ex.Message);
                activity?.SetStatus(ActivityStatusCode.Error, $"Sentiment analysis failed: {ex.Message}");
                // Continue processing even if sentiment analysis fails
                // Save any pending changes (though sentiment won't be set)
                await context.SaveChangesAsync(cancellationToken);
                return false;
            }
        }

        // No text to analyze - this is not an error, just skip sentiment analysis
        return true;
    }
}

