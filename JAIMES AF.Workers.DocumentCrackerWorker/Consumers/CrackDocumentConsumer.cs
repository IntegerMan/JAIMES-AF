using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.Workers.DocumentCrackerWorker.Services;

namespace MattEland.Jaimes.Workers.DocumentCrackerWorker.Consumers;

public class CrackDocumentConsumer(
    IDocumentCrackingService crackingService,
    ILogger<CrackDocumentConsumer> logger,
    ActivitySource activitySource) : IMessageConsumer<CrackDocumentMessage>
{
    public async Task HandleAsync(CrackDocumentMessage message, CancellationToken cancellationToken = default)
    {
        using Activity? activity = activitySource.StartActivity("DocumentCracking.ConsumeMessage");
        activity?.SetTag("messaging.message_type", nameof(CrackDocumentMessage));
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

            await crackingService.ProcessDocumentAsync(message.FilePath, message.RelativeDirectory, cancellationToken);

            logger.LogInformation("Successfully processed document: {FilePath}", message.FilePath);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process crack document message for {FilePath}", message.FilePath);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            
            // Re-throw to let message consumer service handle retry logic
            throw;
        }
    }
}

