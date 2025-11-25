using System.Diagnostics;
using MassTransit;
using Microsoft.Extensions.Logging;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.Workers.DocumentChunking.Services;

namespace MattEland.Jaimes.Workers.DocumentChunking.Consumers;

public class DocumentCrackedConsumer(
    IDocumentChunkingService chunkingService,
    ILogger<DocumentCrackedConsumer> logger,
    ActivitySource activitySource) : IConsumer<DocumentCrackedMessage>
{
    public async Task Consume(ConsumeContext<DocumentCrackedMessage> context)
    {
        DocumentCrackedMessage message = context.Message;

        using Activity? activity = activitySource.StartActivity("DocumentChunking.ConsumeMessage");
        activity?.SetTag("messaging.message_id", context.MessageId?.ToString() ?? "unknown");
        activity?.SetTag("messaging.document_id", message.DocumentId);
        activity?.SetTag("messaging.file_name", message.FileName);

        try
        {
            logger.LogDebug(
                "Received document cracked message: DocumentId={DocumentId}, FileName={FileName}, FilePath={FilePath}",
                message.DocumentId, message.FileName, message.FilePath);

            // Validate message
            if (string.IsNullOrWhiteSpace(message.DocumentId))
            {
                logger.LogError(
                    "Received document cracked message with empty DocumentId. FileName={FileName}, FilePath={FilePath}. " +
                    "Skipping processing to avoid MongoDB errors.",
                    message.FileName, message.FilePath);
                activity?.SetStatus(ActivityStatusCode.Error, "Empty DocumentId");
                // Don't throw - just skip this message to avoid infinite retries
                return;
            }

            await chunkingService.ProcessDocumentAsync(message, context.CancellationToken);

            logger.LogDebug("Successfully processed document chunking: {DocumentId}", message.DocumentId);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process document cracked message for {DocumentId}", message.DocumentId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            
            // Re-throw to let MassTransit handle retry logic
            throw;
        }
    }
}

