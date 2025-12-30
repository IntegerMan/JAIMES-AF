namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Represents details for a single tool call.
/// </summary>
public class ToolCallDetailDto
{
    /// <summary>
    /// The tool call ID.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// The name of the tool.
    /// </summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// When the tool call was made.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// The message ID this tool call is associated with.
    /// </summary>
    public int MessageId { get; init; }

    /// <summary>
    /// The game ID where this tool call occurred.
    /// </summary>
    public Guid? GameId { get; init; }

    /// <summary>
    /// The name of the game (scenario + player).
    /// </summary>
    public string? GameName { get; init; }

    /// <summary>
    /// The name of the agent that made this call.
    /// </summary>
    public string? AgentName { get; init; }

    /// <summary>
    /// The version number of the agent instruction.
    /// </summary>
    public string? AgentVersion { get; init; }

    /// <summary>
    /// Whether the message received positive feedback (true), negative (false), or none (null).
    /// </summary>
    public bool? FeedbackIsPositive { get; init; }

    /// <summary>
    /// The feedback comment, if any.
    /// </summary>
    public string? FeedbackComment { get; init; }
}
