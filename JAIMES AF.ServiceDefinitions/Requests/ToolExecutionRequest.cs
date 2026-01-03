namespace MattEland.Jaimes.ServiceDefinitions.Requests;

/// <summary>
/// Request to execute a tool for testing purposes.
/// </summary>
public record ToolExecutionRequest
{
    /// <summary>
    /// Gets or sets the name of the tool to execute.
    /// </summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// Gets or sets the game ID to use for context (required for tools that need game context).
    /// </summary>
    public Guid? GameId { get; init; }

    /// <summary>
    /// Gets or sets the parameters to pass to the tool.
    /// Keys are parameter names, values are the parameter values as strings (will be converted to appropriate types).
    /// </summary>
    public Dictionary<string, string?> Parameters { get; init; } = new();
}
