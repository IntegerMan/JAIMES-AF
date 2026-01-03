namespace MattEland.Jaimes.ServiceDefinitions.Requests;

/// <summary>
/// Request to run tests against multiple agent versions in a single unified report.
/// </summary>
public class RunMultiVersionTestsRequest
{
    /// <summary>
    /// The agent versions to test.
    /// </summary>
    public List<VersionToTest> Versions { get; set; } = [];

    /// <summary>
    /// Test case IDs to run. If null or empty, runs all active test cases.
    /// </summary>
    public List<int>? TestCaseIds { get; set; }

    /// <summary>
    /// Optional execution name for grouping. Auto-generated if not provided.
    /// </summary>
    public string? ExecutionName { get; set; }

    /// <summary>
    /// Optional list of evaluator names to run. If null or empty, runs all configured evaluators.
    /// </summary>
    public List<string>? EvaluatorNames { get; set; }
}

/// <summary>
/// Identifies a specific agent version to test.
/// </summary>
public class VersionToTest
{
    /// <summary>
    /// The agent ID.
    /// </summary>
    public required string AgentId { get; set; }

    /// <summary>
    /// The instruction version ID to test.
    /// </summary>
    public required int InstructionVersionId { get; set; }
}
