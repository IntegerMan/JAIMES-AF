namespace MattEland.Jaimes.ServiceDefinitions.Services;

/// <summary>
/// Service interface for running agents against test cases in isolation.
/// Messages generated during testing do not go through the standard messaging pipeline
/// and are not persisted to the Messages table.
/// </summary>
public interface IAgentTestRunner
{
    /// <summary>
    /// Runs test cases against a specific agent version.
    /// </summary>
    /// <param name="agentId">The agent to test.</param>
    /// <param name="instructionVersionId">The instruction version to use.</param>
    /// <param name="testCaseIds">Test case IDs to run. If null or empty, runs all active test cases.</param>
    /// <param name="executionName">Optional execution name for grouping. Auto-generated if not provided.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Test run results including generated responses and metrics.</returns>
    Task<TestRunResultResponse> RunTestCasesAsync(
        string agentId,
        int instructionVersionId,
        IEnumerable<int>? testCaseIds = null,
        string? executionName = null,
        CancellationToken ct = default);
}
