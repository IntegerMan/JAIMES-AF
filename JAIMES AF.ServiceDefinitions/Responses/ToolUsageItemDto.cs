namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Represents usage statistics for a single tool.
/// </summary>
public class ToolUsageItemDto
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// Total number of times this tool was invoked.
    /// </summary>
    public int TotalCalls { get; init; }

    /// <summary>
    /// Number of assistant messages where this tool was available.
    /// An assistant message is identified by PlayerId being null.
    /// </summary>
    public int EligibleMessages { get; init; }

    /// <summary>
    /// Percentage of eligible messages that invoked this tool at least once.
    /// Calculated as (DistinctMessagesInvokingTool / EligibleMessages) * 100.
    /// </summary>
    public double UsagePercentage { get; init; }

    /// <summary>
    /// List of agent names and versions where this tool has been used.
    /// </summary>
    public IReadOnlyList<string> EnabledAgents { get; init; } = [];

    /// <summary>
    /// Number of messages invoking this tool that received positive feedback.
    /// </summary>
    public int HelpfulCount { get; init; }

    /// <summary>
    /// Number of messages invoking this tool that received negative feedback.
    /// </summary>
    public int UnhelpfulCount { get; init; }
}

