namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Response DTO for test case run data.
/// </summary>
public record TestCaseRunResponse
{
    public required int Id { get; init; }
    public required int TestCaseId { get; init; }
    public required string TestCaseName { get; init; }
    public required string AgentId { get; init; }
    public string? AgentName { get; init; }
    public required int InstructionVersionId { get; init; }
    public string? VersionNumber { get; init; }
    public required DateTime ExecutedAt { get; init; }
    public required string GeneratedResponse { get; init; }
    public int? DurationMs { get; init; }
    public string? ExecutionName { get; init; }

    // Aggregated metrics
    public List<TestCaseRunMetricResponse> Metrics { get; init; } = [];
}
