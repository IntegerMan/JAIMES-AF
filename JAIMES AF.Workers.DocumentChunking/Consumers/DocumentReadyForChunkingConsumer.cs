using System.Diagnostics;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.Workers.DocumentChunking.Consumers;

public class DocumentReadyForChunkingConsumer(
    IDocumentChunkingService chunkingService,
    ILogger<DocumentReadyForChunkingConsumer> logger,
    ActivitySource activitySource) : IMessageConsumer<DocumentReadyForChunkingMessage>
{
    public async Task HandleAsync(DocumentReadyForChunkingMessage message, CancellationToken cancellationToken = default)
    {
        using Activity? activity = activitySource.StartActivity("DocumentChunking.ConsumeMessage");
        activity?.SetTag("messaging.message_type", nameof(DocumentReadyForChunkingMessage));
        activity?.SetTag("messaging.document_id", message.DocumentId);
        activity?.SetTag("messaging.file_name", message.FileName);

        try
        {
            logger.LogDebug(
                "Received document ready for chunking message: DocumentId={DocumentId}, FileName={FileName}, FilePath={FilePath}",
                message.DocumentId, message.FileName, message.FilePath);

            // Validate message
            if (string.IsNullOrWhiteSpace(message.DocumentId))
            {
                logger.LogError(
                    "Received document ready for chunking message with empty DocumentId. FileName={FileName}, FilePath={FilePath}. " +
                    "Skipping processing to avoid MongoDB errors.",
                    message.FileName, message.FilePath);
                activity?.SetStatus(ActivityStatusCode.Error, "Empty DocumentId");
                // Don't throw - just skip this message to avoid infinite retries
                return;
            }

            await chunkingService.ProcessDocumentAsync(message, cancellationToken);

            logger.LogDebug("Successfully processed document chunking: {DocumentId}", message.DocumentId);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process document ready for chunking message for {DocumentId}", message.DocumentId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            
            // Re-throw to let message consumer service handle retry logic
            throw;
        }
    }
}

