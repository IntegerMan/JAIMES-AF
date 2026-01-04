namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Represents a single record in a JSONL export file for Microsoft Foundry evaluation.
/// Each record contains a user query paired with an agent response, along with optional
/// ground truth and context information.
/// </summary>
public record JsonlExportRecord
{
    /// <summary>
    /// The user's query or message.
    /// </summary>
    public required string Query { get; init; }

    /// <summary>
    /// The expected or reference answer (ground truth), typically from test case descriptions.
    /// Null if no ground truth is available.
    /// </summary>
    public string? GroundTruth { get; init; }

    /// <summary>
    /// The agent's response to the query.
    /// </summary>
    public required string Response { get; init; }

    /// <summary>
    /// Supporting context used by the agent, typically from RAG tool calls.
    /// Formatted as "DocumentName: content text".
    /// Null if no context is available.
    /// </summary>
    public string? Context { get; init; }
}
