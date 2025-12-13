using System.Text.Json;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;

namespace MattEland.Jaimes.Agents.Services;

/// <summary>
/// Factory for creating GameConversationMemoryProvider instances for specific games.
/// </summary>
public class GameConversationMemoryProviderFactory
{
    private readonly IChatHistoryService _chatHistoryService;
    private readonly ILogger<GameConversationMemoryProvider> _logger;

    public GameConversationMemoryProviderFactory(
        IChatHistoryService chatHistoryService,
        ILogger<GameConversationMemoryProvider> logger)
    {
        _chatHistoryService = chatHistoryService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a memory provider for the specified game.
    /// </summary>
    public GameConversationMemoryProvider CreateForGame(Guid gameId)
    {
        return new GameConversationMemoryProvider(gameId, _chatHistoryService, _logger);
    }

    /// <summary>
    /// Creates a memory provider from serialized state (used when deserializing threads).
    /// </summary>
    public GameConversationMemoryProvider CreateFromSerializedState(Guid gameId, JsonElement serializedState)
    {
        return new GameConversationMemoryProvider(gameId, _chatHistoryService, _logger, serializedState);
    }
}

