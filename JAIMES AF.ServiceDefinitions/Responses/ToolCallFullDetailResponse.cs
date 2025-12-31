namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Represents full details for a single tool call, including context and payload.
/// </summary>
public class ToolCallFullDetailResponse : ToolCallDetailDto
{
    /// <summary>
    /// The input JSON arguments provided to the tool.
    /// </summary>
    public string? InputJson { get; init; }

    /// <summary>
    /// The output JSON returned by the tool.
    /// </summary>
    public string? OutputJson { get; init; }

    /// <summary>
    /// The conversation context surrounding this tool call.
    /// This should include the message that made the tool call and preceding messages.
    /// </summary>
    public List<MessageContextDto> ContextMessages { get; init; } = [];
}
