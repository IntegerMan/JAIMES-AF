using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MattEland.Jaimes.Tools;

/// <summary>
/// Tool that provides conversation history search functionality using semantic search.
/// </summary>
public class ConversationSearchTool(GameDto game, IServiceProvider serviceProvider)
{
    private readonly GameDto _game = game ?? throw new ArgumentNullException(nameof(game));

    private readonly IServiceProvider _serviceProvider =
        serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    /// <summary>
    /// Searches the game's conversation history to find relevant past messages.
    /// This tool uses semantic search to find relevant conversation messages from the current game.
    /// Results include the matched message along with the previous and next messages for context.
    /// </summary>
    /// <param name="query">The question or query about past conversations. For example: "What did the player say about the treasure?" or "When did we discuss the dragon?"</param>
    /// <returns>A string containing relevant conversation messages with context (prior and subsequent messages).</returns>
    [Description(
        "Searches the game's conversation history to find relevant past messages. This tool uses semantic search to find conversation messages from the current game that match your query. Results include the matched message along with the previous and next messages for context. Use this tool whenever you need to recall what was said earlier in the conversation, what the player mentioned, or any past events discussed in the game.")]
    public async Task<string> SearchConversationsAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return "Please provide a query or question about the conversation history.";

        Guid gameId = _game.GameId;

        // Create a scope to resolve IConversationSearchService on each call
        // This ensures we get a fresh scoped instance and avoid ObjectDisposedException
        // when the tool outlives the scope that created it
        using IServiceScope scope = _serviceProvider.CreateScope();
        IConversationSearchService? conversationSearchService = scope.ServiceProvider.GetService<IConversationSearchService>();
        if (conversationSearchService == null)
        {
            return "Conversation search service is not available.";
        }

        // Search conversations for the current game
        ConversationSearchResponse response = await conversationSearchService.SearchConversationsAsync(gameId, query, 5);

        if (response.Results.Length == 0) return "No relevant conversation history found for your query.";

        // Format results with context (prior and subsequent messages)
        List<string> resultTexts = new();
        foreach (ConversationSearchResult result in response.Results)
        {
            List<string> messageParts = new();

            // Add previous message if available
            if (result.PreviousMessage != null)
            {
                messageParts.Add($"[Previous] {result.PreviousMessage.ParticipantName}: {result.PreviousMessage.Text}");
            }

            // Add matched message
            messageParts.Add($"[Matched - Relevancy: {result.Relevancy:F2}] {result.MatchedMessage.ParticipantName}: {result.MatchedMessage.Text}");

            // Add next message if available
            if (result.NextMessage != null)
            {
                messageParts.Add($"[Next] {result.NextMessage.ParticipantName}: {result.NextMessage.Text}");
            }

            resultTexts.Add(string.Join("\n", messageParts));
        }

        return string.Join("\n\n---\n\n", resultTexts);
    }
}

