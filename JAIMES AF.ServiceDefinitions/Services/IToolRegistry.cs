namespace MattEland.Jaimes.ServiceDefinitions.Services;

/// <summary>
/// Metadata about a registered tool.
/// </summary>
public class ToolMetadata
{
    /// <summary>
    /// The function name used when invoking the tool.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// A description of what the tool does.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// The category of the tool (e.g., "Player", "Search", "Analysis").
    /// </summary>
    public string? Category { get; init; }
}

/// <summary>
/// Provides metadata about all available tools in the system.
/// </summary>
public interface IToolRegistry
{
    /// <summary>
    /// Gets metadata for all registered tools.
    /// </summary>
    IReadOnlyList<ToolMetadata> GetAllTools();

    /// <summary>
    /// Gets metadata for a specific tool by name.
    /// </summary>
    ToolMetadata? GetTool(string name);
}
