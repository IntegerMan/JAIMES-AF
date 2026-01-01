using Microsoft.AspNetCore.SignalR;

namespace MattEland.Jaimes.ApiService.Hubs;

/// <summary>
/// SignalR hub for real-time message updates.
/// Clients join game-specific groups to receive updates for their active game.
/// </summary>
public class MessageHub : Hub<IMessageHubClient>
{
    private readonly ILogger<MessageHub> _logger;

    public MessageHub(ILogger<MessageHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Called when a client wants to subscribe to updates for a specific game.
    /// </summary>
    /// <param name="gameId">The game ID to subscribe to.</param>
    public async Task JoinGame(Guid gameId)
    {
        string groupName = GetGameGroupName(gameId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogDebug("Client {ConnectionId} joined game group {GameId}", Context.ConnectionId, gameId);
    }

    /// <summary>
    /// Called when a client wants to unsubscribe from updates for a specific game.
    /// </summary>
    /// <param name="gameId">The game ID to unsubscribe from.</param>
    public async Task LeaveGame(Guid gameId)
    {
        string groupName = GetGameGroupName(gameId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogDebug("Client {ConnectionId} left game group {GameId}", Context.ConnectionId, gameId);
    }

    /// <summary>
    /// Called when an admin client wants to subscribe to all updates.
    /// </summary>
    public async Task JoinAdmin()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "admin");
        _logger.LogDebug("Client {ConnectionId} joined admin group", Context.ConnectionId);
    }

    /// <summary>
    /// Called when an admin client wants to unsubscribe from all updates.
    /// </summary>
    public async Task LeaveAdmin()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "admin");
        _logger.LogDebug("Client {ConnectionId} left admin group", Context.ConnectionId);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogDebug("Client {ConnectionId} connected to MessageHub", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogDebug("Client {ConnectionId} disconnected from MessageHub", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Gets the group name for a game.
    /// </summary>
    public static string GetGameGroupName(Guid gameId) => $"game-{gameId}";
}
