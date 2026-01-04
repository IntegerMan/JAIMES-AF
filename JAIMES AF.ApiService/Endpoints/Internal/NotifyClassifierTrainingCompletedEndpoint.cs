using FastEndpoints;
using MattEland.Jaimes.ApiService.Hubs;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using Microsoft.AspNetCore.SignalR;

namespace MattEland.Jaimes.ApiService.Endpoints.Internal;

/// <summary>
/// Internal endpoint for workers to notify when classifier training has completed.
/// This triggers real-time updates to connected admin clients.
/// </summary>
public class NotifyClassifierTrainingCompletedEndpoint : Endpoint<ClassifierTrainingCompletedNotification>
{
    private readonly IHubContext<MessageHub, IMessageHubClient> _hubContext;
    private readonly ILogger<NotifyClassifierTrainingCompletedEndpoint> _logger;

    public NotifyClassifierTrainingCompletedEndpoint(
        IHubContext<MessageHub, IMessageHubClient> hubContext,
        ILogger<NotifyClassifierTrainingCompletedEndpoint> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("/internal/classifier-training-completed");
        AllowAnonymous(); // Internal endpoint - workers don't have auth
        Description(d => d
            .WithTags("Internal")
            .Produces(200)
            .WithSummary("Notifies connected clients about classifier training completion")
            .WithDescription("Called by worker services when classifier training completes."));
    }

    public override async Task HandleAsync(ClassifierTrainingCompletedNotification notification, CancellationToken ct)
    {
        _logger.LogDebug(
            "Broadcasting classifier training completed for job {JobId}, success: {Success}",
            notification.TrainingJobId,
            notification.Success);

        // Notify admin group about training completion
        await _hubContext.Clients.Group("admin").ClassifierTrainingCompleted(notification);

        await Send.NoContentAsync(ct);
    }
}
