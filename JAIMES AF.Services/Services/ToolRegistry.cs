using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ServiceLayer.Services;

/// <summary>
/// Registry of all available tools in the system.
/// This provides a single source of truth for tool metadata.
/// </summary>
public class ToolRegistry : IToolRegistry
{
    private static readonly List<ToolMetadata> AllTools =
    [
        new ToolMetadata
        {
            Name = "GetPlayerInfo",
            Description = "Retrieves detailed information about the current player character in the game, including their name, unique identifier, and character description.",
            Category = "Player"
        },
        new ToolMetadata
        {
            Name = "SearchRules",
            Description = "Searches the ruleset's indexed rules to find answers to specific questions or queries about game mechanics and rules.",
            Category = "Search"
        },
        new ToolMetadata
        {
            Name = "SearchConversations",
            Description = "Searches the game's conversation history to find relevant past messages using semantic search.",
            Category = "Search"
        },
        new ToolMetadata
        {
            Name = "GetPlayerSentiment",
            Description = "Retrieves the last 5 most recent sentiment analysis results for the player in the current game.",
            Category = "Analysis"
        }
    ];

    /// <inheritdoc />
    public IReadOnlyList<ToolMetadata> GetAllTools() => AllTools.AsReadOnly();

    /// <inheritdoc />
    public ToolMetadata? GetTool(string name) =>
        AllTools.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
}
