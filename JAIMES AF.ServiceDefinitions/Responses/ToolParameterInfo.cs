namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Represents metadata about a tool parameter.
/// </summary>
public record ToolParameterInfo
{
    /// <summary>
    /// Gets or sets the name of the parameter.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the type name of the parameter (e.g., "string", "int", "bool").
    /// </summary>
    public required string TypeName { get; init; }

    /// <summary>
    /// Gets or sets whether this parameter is required.
    /// </summary>
    public required bool IsRequired { get; init; }

    /// <summary>
    /// Gets or sets the description of the parameter, if available.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets or sets the default value of the parameter, if any.
    /// </summary>
    public string? DefaultValue { get; init; }
}
