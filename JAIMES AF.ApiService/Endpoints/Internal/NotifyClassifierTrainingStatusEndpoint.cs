using FastEndpoints;
using MattEland.Jaimes.ApiService.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace MattEland.Jaimes.ApiService.Endpoints.Internal;

/// <summary>
/// Internal endpoint for workers to notify when classifier training status changes.
/// This triggers real-time updates to connected admin clients.
/// </summary>
public class NotifyClassifierTrainingStatusEndpoint : Endpoint<ClassifierTrainingStatusRequest>
{
    private readonly IHubContext<MessageHub, IMessageHubClient> _hubContext;
    private readonly ILogger<NotifyClassifierTrainingStatusEndpoint> _logger;

    public NotifyClassifierTrainingStatusEndpoint(
        IHubContext<MessageHub, IMessageHubClient> hubContext,
        ILogger<NotifyClassifierTrainingStatusEndpoint> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("/internal/classifier-training-status");
        AllowAnonymous(); // Internal endpoint - workers don't have auth
        Description(d => d
            .WithTags("Internal")
            .Produces(204)
            .WithSummary("Notifies connected clients about classifier training status change")
            .WithDescription("Called by worker services when classifier training status changes (e.g., Queued → Training)."));
    }

    public override async Task HandleAsync(ClassifierTrainingStatusRequest req, CancellationToken ct)
    {
        _logger.LogDebug(
            "Broadcasting classifier training status changed for job {JobId}, status: {Status}",
            req.TrainingJobId,
            req.Status);

        // Notify admin group about training status change
        await _hubContext.Clients.Group("admin").ClassifierTrainingStatusChanged(req.TrainingJobId, req.Status);

        await Send.NoContentAsync(ct);
    }
}

/// <summary>
/// Request for classifier training status change notification.
/// </summary>
public record ClassifierTrainingStatusRequest(int TrainingJobId, string Status);
