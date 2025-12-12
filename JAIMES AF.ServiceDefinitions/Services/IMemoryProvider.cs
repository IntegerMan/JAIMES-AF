using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace MattEland.Jaimes.ServiceDefinitions.Services;

/// <summary>
/// Provides an abstraction for persisting and retrieving agent conversation memory.
/// This interface follows the Agent Framework's MemoryProvider pattern for managing long-term conversation state.
/// </summary>
public interface IMemoryProvider
{
    /// <summary>
    /// Loads the most recent conversation thread for a game.
    /// </summary>
    /// <param name="gameId">The unique identifier of the game.</param>
    /// <param name="agent">The agent that will deserialize the thread.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deserialized thread, or a new thread if no history exists.</returns>
    Task<AgentThread> LoadThreadAsync(Guid gameId, AIAgent agent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves conversation messages and thread state after an agent interaction.
    /// </summary>
    /// <param name="gameId">The unique identifier of the game.</param>
    /// <param name="playerId">The unique identifier of the player.</param>
    /// <param name="userMessage">The user's message (if any).</param>
    /// <param name="assistantMessages">The assistant's response messages.</param>
    /// <param name="thread">The updated thread state to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SaveConversationAsync(
        Guid gameId,
        string playerId,
        ChatMessage? userMessage,
        IEnumerable<ChatMessage> assistantMessages,
        AgentThread thread,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the conversation history for a game as a collection of messages.
    /// </summary>
    /// <param name="gameId">The unique identifier of the game.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of messages in chronological order.</returns>
    Task<IEnumerable<ChatMessage>> GetConversationHistoryAsync(Guid gameId, CancellationToken cancellationToken = default);
}
