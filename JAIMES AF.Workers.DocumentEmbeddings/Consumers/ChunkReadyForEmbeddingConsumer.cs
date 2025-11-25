using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.Workers.DocumentEmbeddings.Services;

namespace MattEland.Jaimes.Workers.DocumentEmbeddings.Consumers;

public class ChunkReadyForEmbeddingConsumer(
    IDocumentEmbeddingService embeddingService,
    ILogger<ChunkReadyForEmbeddingConsumer> logger,
    ActivitySource activitySource) : IMessageConsumer<ChunkReadyForEmbeddingMessage>
{
    public async Task HandleAsync(ChunkReadyForEmbeddingMessage message, CancellationToken cancellationToken = default)
    {
        using Activity? activity = activitySource.StartActivity("DocumentEmbedding.ConsumeChunkMessage");
        activity?.SetTag("messaging.message_type", nameof(ChunkReadyForEmbeddingMessage));
        activity?.SetTag("messaging.chunk_id", message.ChunkId);
        activity?.SetTag("messaging.document_id", message.DocumentId);

        try
        {
            logger.LogInformation(
                "Received chunk ready for embedding: ChunkId={ChunkId}, DocumentId={DocumentId}, ChunkIndex={ChunkIndex}",
                message.ChunkId, message.DocumentId, message.ChunkIndex);

            // Validate message
            if (string.IsNullOrWhiteSpace(message.ChunkId) || string.IsNullOrWhiteSpace(message.DocumentId))
            {
                logger.LogError(
                    "Received chunk message with empty ChunkId or DocumentId. ChunkId={ChunkId}, DocumentId={DocumentId}. " +
                    "Skipping processing.",
                    message.ChunkId, message.DocumentId);
                activity?.SetStatus(ActivityStatusCode.Error, "Empty ChunkId or DocumentId");
                return;
            }

            await embeddingService.ProcessChunkAsync(message, cancellationToken);

            logger.LogInformation("Successfully processed chunk embedding: {ChunkId}", message.ChunkId);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process chunk embedding message for {ChunkId}", message.ChunkId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            
            // Re-throw to let message consumer service handle retry logic
            throw;
        }
    }
}

