using FastEndpoints;
using MattEland.Jaimes.ApiService.Hubs;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using Microsoft.AspNetCore.SignalR;

namespace MattEland.Jaimes.ApiService.Endpoints.Internal;

/// <summary>
/// Internal endpoint for workers to notify the hub when a message has been processed.
/// This triggers real-time updates to connected web clients.
/// </summary>
public class NotifyMessageUpdateEndpoint : Endpoint<MessageUpdateNotification>
{
    private readonly IHubContext<MessageHub, IMessageHubClient> _hubContext;
    private readonly ILogger<NotifyMessageUpdateEndpoint> _logger;

    public NotifyMessageUpdateEndpoint(
        IHubContext<MessageHub, IMessageHubClient> hubContext,
        ILogger<NotifyMessageUpdateEndpoint> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("/internal/message-updates");
        AllowAnonymous(); // Internal endpoint - workers don't have auth
        Description(d => d
            .WithTags("Internal")
            .Produces(200)
            .WithSummary("Notifies connected clients about a message update")
            .WithDescription("Called by worker services when message processing (sentiment or metrics) completes."));
    }

    public override async Task HandleAsync(MessageUpdateNotification notification, CancellationToken ct)
    {
        string groupName = MessageHub.GetGameGroupName(notification.GameId);

        _logger.LogDebug(
            "Broadcasting {UpdateType} update for message {MessageId} to game group {GameId}",
            notification.UpdateType,
            notification.MessageId,
            notification.GameId);

        await _hubContext.Clients.Group(groupName).MessageUpdated(notification);

        await Send.NoContentAsync(ct);
    }
}
