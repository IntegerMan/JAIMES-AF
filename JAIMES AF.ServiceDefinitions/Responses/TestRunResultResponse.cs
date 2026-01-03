namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Response for a test run execution.
/// </summary>
public record TestRunResultResponse
{
    public required string ExecutionName { get; init; }
    public required string AgentId { get; init; }
    public string? AgentName { get; init; }
    public required int InstructionVersionId { get; init; }
    public string? VersionNumber { get; init; }
    public required DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public required int TotalTestCases { get; init; }
    public required int CompletedTestCases { get; init; }
    public required int FailedTestCases { get; init; }

    /// <summary>
    /// Average score across all metrics and test cases.
    /// </summary>
    public double? AverageScore { get; init; }

    /// <summary>
    /// Individual test case run results.
    /// </summary>
    public List<TestCaseRunResponse> Runs { get; init; } = [];
}
