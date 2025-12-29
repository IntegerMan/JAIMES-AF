using MattEland.Jaimes.ServiceDefinitions.Messages;
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
    SentimentModelService sentimentModelService) : IMessageConsumer<ConversationMessageQueuedMessage>
{
    public async Task HandleAsync(ConversationMessageQueuedMessage message, CancellationToken cancellationToken = default)
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

            // Load message from database
            await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
            Message? messageEntity = await context.Messages
                .Include(m => m.Game)
                .Include(m => m.Player)
                .FirstOrDefaultAsync(m => m.Id == message.MessageId, cancellationToken);

            if (messageEntity == null)
            {
                logger.LogWarning(
                    "Message {MessageId} not found in database. It may have been deleted.",
                    message.MessageId);
                activity?.SetStatus(ActivityStatusCode.Error, "Message not found");
                return;
            }

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

            bool sentimentAnalysisSucceeded = await AnalyzeSentimentAsync(messageEntity, activity, context, cancellationToken);

            // Only set status to Ok if sentiment analysis succeeded or wasn't attempted
            // If sentiment analysis failed, the activity status was already set to Error in AnalyzeSentimentAsync
            if (sentimentAnalysisSucceeded)
            {
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process user message {MessageId}", message.MessageId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            // Re-throw to let message consumer service handle retry logic
            throw;
        }
    }

    private async Task<bool> AnalyzeSentimentAsync(Message messageEntity, Activity? activity, JaimesDbContext context,
        CancellationToken cancellationToken)
    {
        // Perform sentiment analysis
        if (!string.IsNullOrWhiteSpace(messageEntity.Text))
        {
            try
            {
                double confidenceThreshold = sentimentOptions.Value.ConfidenceThreshold;
                (int sentiment, double confidence) = sentimentModelService.AnalyzeSentimentWithThreshold(
                    messageEntity.Text,
                    confidenceThreshold);

                activity?.SetTag("sentiment.value", sentiment);
                activity?.SetTag("sentiment.confidence", confidence);
                activity?.SetTag("sentiment.threshold", confidenceThreshold);

                messageEntity.Sentiment = sentiment;

                logger.LogInformation(
                    "Sentiment analysis completed - MessageId: {MessageId}, Sentiment: {Sentiment}, Confidence: {Confidence}, Threshold: {Threshold}",
                    messageEntity.Id,
                    sentiment,
                    confidence,
                    confidenceThreshold);

                // Save sentiment to database
                await context.SaveChangesAsync(cancellationToken);
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

