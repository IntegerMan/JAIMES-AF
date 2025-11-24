using System.Diagnostics;
using MassTransit;
using Microsoft.Extensions.Logging;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.Workers.DocumentCracker.Services;

namespace MattEland.Jaimes.Workers.DocumentCracker.Consumers;

public class CrackDocumentConsumer(
    IDocumentCrackingService crackingService,
    ILogger<CrackDocumentConsumer> logger,
    ActivitySource activitySource) : IConsumer<CrackDocumentMessage>
{
    public async Task Consume(ConsumeContext<CrackDocumentMessage> context)
    {
        CrackDocumentMessage message = context.Message;

        using Activity? activity = activitySource.StartActivity("DocumentCracking.ConsumeMessage");
        activity?.SetTag("messaging.message_id", context.MessageId?.ToString() ?? "unknown");
        activity?.SetTag("messaging.file_path", message.FilePath);

        try
        {
            logger.LogInformation(
                "Received crack document message: FilePath={FilePath}, RelativeDirectory={RelativeDirectory}",
                message.FilePath, message.RelativeDirectory);

            // Validate message
            if (string.IsNullOrWhiteSpace(message.FilePath))
            {
                logger.LogError(
                    "Received crack document message with empty FilePath. Skipping processing.");
                activity?.SetStatus(ActivityStatusCode.Error, "Empty FilePath");
                // Don't throw - just skip this message to avoid infinite retries
                return;
            }

            if (!File.Exists(message.FilePath))
            {
                logger.LogWarning(
                    "Received crack document message for non-existent file: {FilePath}. Skipping processing.",
                    message.FilePath);
                activity?.SetStatus(ActivityStatusCode.Error, "File not found");
                // Don't throw - just skip this message to avoid infinite retries
                return;
            }

            await crackingService.ProcessDocumentAsync(message.FilePath, message.RelativeDirectory, context.CancellationToken);

            logger.LogInformation("Successfully processed document: {FilePath}", message.FilePath);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process crack document message for {FilePath}", message.FilePath);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            
            // Re-throw to let MassTransit handle retry logic
            throw;
        }
    }
}






