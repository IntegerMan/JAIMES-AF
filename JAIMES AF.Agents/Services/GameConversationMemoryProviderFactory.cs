using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MattEland.Jaimes.Agents.Services;

/// <summary>
/// Factory for creating GameConversationMemoryProvider instances for specific games.
/// </summary>
public class GameConversationMemoryProviderFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<GameConversationMemoryProvider> _logger;

    public GameConversationMemoryProviderFactory(
        IServiceProvider serviceProvider,
        ILogger<GameConversationMemoryProvider> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Creates a memory provider for the specified game.
    /// </summary>
    /// <param name="gameId">The game ID</param>
    /// <param name="rootServiceProvider">Optional root service provider. If not provided, uses the factory's service provider.</param>
    public GameConversationMemoryProvider CreateForGame(Guid gameId, IServiceProvider? rootServiceProvider = null)
    {
        IServiceProvider serviceProvider = rootServiceProvider ?? _serviceProvider;
        return new GameConversationMemoryProvider(gameId, serviceProvider, _logger);
    }

    /// <summary>
    /// Creates a memory provider from serialized state (used when deserializing threads).
    /// </summary>
    /// <param name="gameId">The game ID</param>
    /// <param name="serializedState">The serialized state</param>
    /// <param name="rootServiceProvider">Optional root service provider. If not provided, uses the factory's service provider.</param>
    public GameConversationMemoryProvider CreateFromSerializedState(Guid gameId, JsonElement serializedState, IServiceProvider? rootServiceProvider = null)
    {
        IServiceProvider serviceProvider = rootServiceProvider ?? _serviceProvider;
        return new GameConversationMemoryProvider(gameId, serviceProvider, _logger, serializedState);
    }
}

