namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Response DTO for test case run metric data.
/// </summary>
public record TestCaseRunMetricResponse
{
    public required int Id { get; init; }
    public required string MetricName { get; init; }
    public required double Score { get; init; }
    public string? Remarks { get; init; }
    public int? EvaluatorId { get; init; }
    public string? EvaluatorName { get; init; }
}
