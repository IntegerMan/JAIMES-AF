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
    public required string AgentId { get; init; }
    public required string AgentName { get; init; }
    public required int InstructionVersionId { get; init; }
    public required string VersionNumber { get; init; }
    public int TestCaseCount { get; init; }
}
