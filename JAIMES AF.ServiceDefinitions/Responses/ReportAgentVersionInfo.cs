namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Represents an agent version within a stored test report.
/// </summary>
public record ReportAgentVersionInfo
{
    /// <summary>
    /// The agent's unique identifier.
    /// </summary>
    public required string AgentId { get; init; }

    /// <summary>
    /// Display name for the agent.
    /// </summary>
    public required string AgentName { get; init; }

    /// <summary>
    /// The instruction version ID.
    /// </summary>
    public required int InstructionVersionId { get; init; }

    /// <summary>
    /// Display version number (e.g., "v1.0", "v2.3").
    /// </summary>
    public required string VersionNumber { get; init; }

    /// <summary>
    /// Number of test cases run with this agent version.
    /// </summary>
    public int TestCaseCount { get; init; }
}
