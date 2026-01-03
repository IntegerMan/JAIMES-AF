namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Response containing a list of available tools for testing.
/// </summary>
public record ToolMetadataListResponse
{
    /// <summary>
    /// Gets or sets the list of available tools.
    /// </summary>
    public required ToolMetadataResponse[] Tools { get; init; }
}
