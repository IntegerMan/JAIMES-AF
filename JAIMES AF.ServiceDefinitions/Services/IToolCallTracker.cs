namespace MattEland.Jaimes.ServiceDefinitions.Services;

/// <summary>
/// Service for tracking tool calls during an agent run.
/// Tool calls are stored in memory during the request and then persisted to the database.
/// </summary>
public interface IToolCallTracker
{
    /// <summary>
    /// Records a tool call with its input and output.
    /// </summary>
    /// <param name="toolName">The name of the tool that was called.</param>
    /// <param name="input">The input arguments passed to the tool (will be serialized to JSON).</param>
    /// <param name="output">The output result from the tool (will be serialized to JSON).</param>
    Task RecordToolCallAsync(string toolName, object? input, object? output);

    /// <summary>
    /// Gets all tool calls recorded during the current request.
    /// </summary>
    /// <returns>A list of tool call records.</returns>
    Task<IReadOnlyList<ToolCallRecord>> GetToolCallsAsync();

    /// <summary>
    /// Clears all recorded tool calls (typically called after persistence).
    /// </summary>
    Task ClearAsync();
}

/// <summary>
/// Represents a single tool call record.
/// </summary>
public record ToolCallRecord
{
    public required string ToolName { get; init; }
    public string? InputJson { get; init; }
    public string? OutputJson { get; init; }
    public DateTime CreatedAt { get; init; }
}


