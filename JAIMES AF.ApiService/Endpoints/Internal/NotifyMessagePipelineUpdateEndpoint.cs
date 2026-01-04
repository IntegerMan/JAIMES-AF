using FastEndpoints;
using MattEland.Jaimes.ApiService.Hubs;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using Microsoft.AspNetCore.SignalR;

namespace MattEland.Jaimes.ApiService.Endpoints.Internal;

/// <summary>
/// Internal endpoint for workers to notify the hub when a message processing stage changes.
/// This triggers real-time updates to connected web clients showing pipeline progress.
/// </summary>
public class NotifyMessagePipelineUpdateEndpoint : Endpoint<MessagePipelineStageNotification>
{
    private readonly IHubContext<PipelineStatusHub, IPipelineStatusHubClient> _hubContext;
    private readonly ILogger<NotifyMessagePipelineUpdateEndpoint> _logger;

    public NotifyMessagePipelineUpdateEndpoint(
        IHubContext<PipelineStatusHub, IPipelineStatusHubClient> hubContext,
        ILogger<NotifyMessagePipelineUpdateEndpoint> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("/internal/message-pipeline-updates");
        AllowAnonymous(); // Internal endpoint - workers don't have auth
        Description(d => d
            .WithTags("Internal")
            .Produces(200)
            .WithSummary("Notifies connected clients about a message pipeline stage update")
            .WithDescription("Called by worker services when message processing stages start, complete, or fail."));
    }

    public override async Task HandleAsync(MessagePipelineStageNotification notification, CancellationToken ct)
    {
        _logger.LogDebug(
            "Broadcasting {PipelineType} pipeline stage {Stage} ({StageStatus}) for message {MessageId}",
            notification.PipelineType,
            notification.Stage,
            notification.StageStatus,
            notification.MessageId);

        // Broadcast to all clients subscribed to message pipeline status
        await _hubContext.Clients.Group("message-pipeline-status").MessageStageUpdated(notification);

        await Send.NoContentAsync(ct);
    }
}
