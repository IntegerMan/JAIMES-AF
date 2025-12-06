namespace MattEland.Jaimes.Workers.DocumentEmbedding.Consumers;

public class ChunkReadyForEmbeddingConsumer(
    IDocumentEmbeddingService embeddingService,
    ILogger<ChunkReadyForEmbeddingConsumer> logger,
    ActivitySource activitySource) : IMessageConsumer<ChunkReadyForEmbeddingMessage>
{
    public async Task HandleAsync(ChunkReadyForEmbeddingMessage message, CancellationToken cancellationToken = default)
    {
        using Activity? activity = activitySource.StartActivity("DocumentEmbedding.ConsumeMessage");
        activity?.SetTag("messaging.message_type", nameof(ChunkReadyForEmbeddingMessage));
        activity?.SetTag("messaging.chunk_id", message.ChunkId);
        activity?.SetTag("messaging.document_id", message.DocumentId);

        try
        {
            logger.LogDebug(
                "Received chunk ready for embedding message: ChunkId={ChunkId}, DocumentId={DocumentId}, FileName={FileName}",
                message.ChunkId,
                message.DocumentId,
                message.FileName);

            // Validate message
            if (string.IsNullOrWhiteSpace(message.ChunkId))
            {
                logger.LogError(
                    "Received chunk ready for embedding message with empty ChunkId. DocumentId={DocumentId}, FileName={FileName}. " +
                    "Skipping processing to avoid errors.",
                    message.DocumentId,
                    message.FileName);
                activity?.SetStatus(ActivityStatusCode.Error, "Empty ChunkId");
                // Don't throw - just skip this message to avoid infinite retries
                return;
            }

            if (string.IsNullOrWhiteSpace(message.ChunkText))
            {
                logger.LogError(
                    "Received chunk ready for embedding message with empty ChunkText. ChunkId={ChunkId}, DocumentId={DocumentId}. " +
                    "Skipping processing.",
                    message.ChunkId,
                    message.DocumentId);
                activity?.SetStatus(ActivityStatusCode.Error, "Empty ChunkText");
                return;
            }

            await embeddingService.ProcessChunkAsync(message, cancellationToken);

            logger.LogDebug("Successfully processed chunk embedding: {ChunkId}", message.ChunkId);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process chunk ready for embedding message for {ChunkId}", message.ChunkId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            // Re-throw to let message consumer service handle retry logic
            throw;
        }
    }
}