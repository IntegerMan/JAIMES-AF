using System.ComponentModel.DataAnnotations;

namespace MattEland.Jaimes.ServiceDefinitions.Requests;

/// <summary>
/// Request to run test cases against an agent version.
/// </summary>
public record RunTestCasesRequest
{
    /// <summary>
    /// Optional list of test case IDs to run. If null or empty, all active test cases will be run.
    /// </summary>
    public List<int>? TestCaseIds { get; init; }

    /// <summary>
    /// Optional execution name for grouping results. If not provided, a name will be auto-generated.
    /// </summary>
    [MaxLength(250)]
    public string? ExecutionName { get; init; }
}
