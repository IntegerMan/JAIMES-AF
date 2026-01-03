namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Response for a multi-version test run execution.
/// </summary>
public record MultiVersionTestRunResponse
{
    /// <summary>
    /// The shared execution name for all versions.
    /// </summary>
    public required string ExecutionName { get; init; }

    /// <summary>
    /// When the test run started.
    /// </summary>
    public required DateTime StartedAt { get; init; }

    /// <summary>
    /// When the test run completed.
    /// </summary>
    public DateTime? CompletedAt { get; init; }

    /// <summary>
    /// Total number of version/test-case combinations.
    /// </summary>
    public required int TotalRuns { get; init; }

    /// <summary>
    /// Number of completed runs.
    /// </summary>
    public required int CompletedRuns { get; init; }

    /// <summary>
    /// Number of failed runs.
    /// </summary>
    public required int FailedRuns { get; init; }

    /// <summary>
    /// Average score across all metrics and runs.
    /// </summary>
    public double? AverageScore { get; init; }

    /// <summary>
    /// Results grouped by version.
    /// </summary>
    public List<TestRunResultResponse> VersionResults { get; init; } = [];

    /// <summary>
    /// ID of the stored report file, if generated.
    /// </summary>
    public int? ReportFileId { get; init; }
}
