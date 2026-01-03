namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Represents metadata about a registered tool that can be tested.
/// </summary>
public record ToolMetadataResponse
{
    /// <summary>
    /// Gets or sets the unique name of the tool.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the description of the tool.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets or sets the category of the tool.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Gets or sets the name of the class that implements this tool.
    /// </summary>
    public required string ClassName { get; init; }

    /// <summary>
    /// Gets or sets the name of the method that implements this tool.
    /// </summary>
    public required string MethodName { get; init; }

    /// <summary>
    /// Gets or sets the list of parameters for this tool.
    /// </summary>
    public required ToolParameterInfo[] Parameters { get; init; }

    /// <summary>
    /// Gets or sets whether this tool requires a game context to execute.
    /// </summary>
    public required bool RequiresGameContext { get; init; }
}
