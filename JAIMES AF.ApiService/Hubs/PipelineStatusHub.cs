using Microsoft.AspNetCore.SignalR;

namespace MattEland.Jaimes.ApiService.Hubs;

/// <summary>
/// SignalR hub for real-time document and message pipeline status updates.
/// Clients can subscribe to receive updates about document processing queue sizes
/// and message processing stage progress.
/// </summary>
public class PipelineStatusHub : Hub<IPipelineStatusHubClient>
{
    private readonly ILogger<PipelineStatusHub> _logger;

    public PipelineStatusHub(ILogger<PipelineStatusHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Called when a client wants to subscribe to document pipeline status updates.
    /// </summary>
    public async Task SubscribeToPipelineStatus()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "pipeline-status");
        _logger.LogDebug("Client {ConnectionId} subscribed to pipeline status updates", Context.ConnectionId);
    }

    /// <summary>
    /// Called when a client wants to unsubscribe from document pipeline status updates.
    /// </summary>
    public async Task UnsubscribeFromPipelineStatus()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "pipeline-status");
        _logger.LogDebug("Client {ConnectionId} unsubscribed from pipeline status updates", Context.ConnectionId);
    }

    /// <summary>
    /// Called when a client wants to subscribe to message pipeline status updates.
    /// </summary>
    public async Task SubscribeToMessagePipelineStatus()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "message-pipeline-status");
        _logger.LogDebug("Client {ConnectionId} subscribed to message pipeline status updates", Context.ConnectionId);
    }

    /// <summary>
    /// Called when a client wants to unsubscribe from message pipeline status updates.
    /// </summary>
    public async Task UnsubscribeFromMessagePipelineStatus()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "message-pipeline-status");
        _logger.LogDebug("Client {ConnectionId} unsubscribed from message pipeline status updates", Context.ConnectionId);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogDebug("Client {ConnectionId} connected to PipelineStatusHub", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogDebug("Client {ConnectionId} disconnected from PipelineStatusHub", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
