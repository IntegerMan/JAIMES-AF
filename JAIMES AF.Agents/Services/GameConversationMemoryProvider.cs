using System.Text.Json;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace MattEland.Jaimes.Agents.Services;

/// <summary>
/// A custom AIContextProvider that manages long-term conversation history for games.
/// Automatically persists thread state to the database after each agent interaction.
/// </summary>
public class GameConversationMemoryProvider : AIContextProvider
{
    private readonly Guid _gameId;
    private readonly IChatHistoryService _chatHistoryService;
    private readonly ILogger<GameConversationMemoryProvider> _logger;
    private AgentThread? _cachedThread;

    public GameConversationMemoryProvider(
        Guid gameId,
        IChatHistoryService chatHistoryService,
        ILogger<GameConversationMemoryProvider> logger)
    {
        _gameId = gameId;
        _chatHistoryService = chatHistoryService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a provider from serialized state (used when deserializing threads).
    /// </summary>
    public GameConversationMemoryProvider(
        Guid gameId,
        IChatHistoryService chatHistoryService,
        ILogger<GameConversationMemoryProvider> logger,
        JsonElement serializedState)
        : this(gameId, chatHistoryService, logger)
    {
        // The serialized state is not used here - thread deserialization happens at the agent level
        // This constructor is called when the thread is deserialized, but we don't need to do anything
    }

    /// <summary>
    /// Sets the thread reference for this provider. Called when the provider is attached to a thread.
    /// </summary>
    public void SetThread(AgentThread thread)
    {
        _cachedThread = thread;
    }

    /// <summary>
    /// Called before the agent processes messages.
    /// The thread is already loaded/created by GameAwareAgent, so we just track it here.
    /// </summary>
    public override ValueTask<AIContext> InvokingAsync(InvokingContext context, CancellationToken cancellationToken = default)
    {
        // Store thread reference if available
        // Note: The thread might not be directly accessible from context, so we rely on SetThread being called
        // The thread is already loaded by GameAwareAgent before it reaches here
        // We don't need to do anything in InvokingAsync - just return empty context
        // The actual thread loading happens in GetOrCreateGameThreadAsync
        return ValueTask.FromResult(new AIContext());
    }

    /// <summary>
    /// Called after the agent processes messages. Automatically persists conversation state to database.
    /// Note: This method may not be called automatically depending on Agent Framework version.
    /// The persistence is also handled manually from PersistGameStateAsync for reliability.
    /// </summary>
    public override async ValueTask InvokedAsync(InvokedContext context, CancellationToken cancellationToken = default)
    {
        // Use cached thread reference since context doesn't provide direct thread access
        if (_cachedThread == null)
        {
            _logger.LogWarning("Cannot persist thread state - thread is null for game {GameId}", _gameId);
            return;
        }

        // Automatically serialize and save thread state after each agent interaction
        await SaveThreadStateAsync(_cachedThread, null, cancellationToken);
    }

    /// <summary>
    /// Manually saves the thread state. This can be called from PersistGameStateAsync
    /// to ensure persistence even if InvokedAsync is not called automatically.
    /// </summary>
    public async Task SaveThreadStateManuallyAsync(AgentThread thread, int? messageId = null, CancellationToken cancellationToken = default)
    {
        await SaveThreadStateAsync(thread, messageId, cancellationToken);
    }

    /// <summary>
    /// Saves the current thread state to the database.
    /// </summary>
    private async Task SaveThreadStateAsync(AgentThread thread, int? messageId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            string threadJson = thread.Serialize(JsonSerializerOptions.Web).GetRawText();
            _logger.LogDebug("Saving thread state for game {GameId}, length: {Length} characters", _gameId, threadJson.Length);
            
            await _chatHistoryService.SaveThreadJsonAsync(_gameId, threadJson, messageId, cancellationToken);
            _logger.LogInformation("Saved thread state for game {GameId}", _gameId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving thread state for game {GameId}", _gameId);
            // Don't throw - we don't want to break the agent response if persistence fails
        }
    }
}

