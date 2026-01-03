namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Response from executing a tool for testing purposes.
/// </summary>
public record ToolExecutionResponse
{
    /// <summary>
    /// Gets or sets whether the tool executed successfully.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Gets or sets the result of the tool execution.
    /// This may be a string or JSON-serialized object.
    /// </summary>
    public string? Result { get; init; }

    /// <summary>
    /// Gets or sets the error message if the execution failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets or sets the execution time in milliseconds.
    /// </summary>
    public required long ExecutionTimeMs { get; init; }

    /// <summary>
    /// Gets or sets the name of the tool that was executed.
    /// </summary>
    public required string ToolName { get; init; }
}
