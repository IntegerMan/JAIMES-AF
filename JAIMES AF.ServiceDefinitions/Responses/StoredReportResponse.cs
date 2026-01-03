namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Response DTO for stored test reports.
/// </summary>
public record StoredReportResponse
{
    public required int ReportId { get; init; }
    public required string FileName { get; init; }
    public required DateTime CreatedAt { get; init; }
    public long? SizeBytes { get; init; }

    // Related run info
    public required string ExecutionName { get; init; }

    /// <summary>
    /// All agent versions included in this report.
    /// </summary>
    public List<ReportAgentVersionInfo> AgentVersions { get; init; } = [];

    /// <summary>
    /// Total number of test cases across all agent versions.
    /// </summary>
    public int TotalTestCaseCount { get; init; }

    /// <summary>
    /// Number of unique evaluators that produced metrics for this report.
    /// </summary>
    public int EvaluatorCount { get; init; }

    /// <summary>
    /// True if the report file was deleted but the metrics remain.
    /// </summary>
    public bool IsDeleted { get; init; }
}
