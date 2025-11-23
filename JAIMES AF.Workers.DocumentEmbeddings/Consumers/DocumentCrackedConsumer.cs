using System.Diagnostics;
using MassTransit;
using Microsoft.Extensions.Logging;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.Workers.DocumentEmbeddings.Services;

namespace MattEland.Jaimes.Workers.DocumentEmbeddings.Consumers;

public class DocumentCrackedConsumer(
    IDocumentEmbeddingService embeddingService,
    ILogger<DocumentCrackedConsumer> logger,
    ActivitySource activitySource) : IConsumer<DocumentCrackedMessage>
{
    public async Task Consume(ConsumeContext<DocumentCrackedMessage> context)
    {
        DocumentCrackedMessage message = context.Message;

        using Activity? activity = activitySource.StartActivity("DocumentEmbedding.ConsumeMessage");
        activity?.SetTag("messaging.message_id", context.MessageId?.ToString() ?? "unknown");
        activity?.SetTag("messaging.document_id", message.DocumentId);
        activity?.SetTag("messaging.file_name", message.FileName);

        try
        {
            logger.LogInformation(
                "Received document cracked message: DocumentId={DocumentId}, FileName={FileName}, FilePath={FilePath}",
                message.DocumentId, message.FileName, message.FilePath);

            await embeddingService.ProcessDocumentAsync(message, context.CancellationToken);

            logger.LogInformation("Successfully processed document embedding: {DocumentId}", message.DocumentId);
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

