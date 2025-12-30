namespace MattEland.Jaimes.ServiceDefinitions.Requests;

/// <summary>
/// Request to get metadata (feedback, tool calls, metrics) for multiple messages.
/// </summary>
public record MessagesMetadataRequest
{
    /// <summary>
    /// The IDs of the messages to retrieve metadata for.
    /// </summary>
    public required List<int> MessageIds { get; init; }
}
