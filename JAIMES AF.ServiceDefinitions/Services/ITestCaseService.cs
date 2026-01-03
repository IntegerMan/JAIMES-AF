namespace MattEland.Jaimes.ServiceDefinitions.Services;

/// <summary>
/// Service interface for managing test cases.
/// </summary>
public interface ITestCaseService
{
    /// <summary>
    /// Gets a test case by its ID.
    /// </summary>
    Task<TestCaseResponse?> GetTestCaseAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Gets a test case by the message ID it references.
    /// </summary>
    Task<TestCaseResponse?> GetTestCaseByMessageIdAsync(int messageId, CancellationToken ct = default);

    /// <summary>
    /// Lists all test cases, optionally filtered by agent ID.
    /// </summary>
    Task<List<TestCaseResponse>> ListTestCasesAsync(string? agentId = null, bool includeInactive = false,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a test case from a player message.
    /// </summary>
    Task<TestCaseResponse> CreateTestCaseAsync(int messageId, string name, string? description,
        CancellationToken ct = default);

    /// <summary>
    /// Soft deletes a test case (sets IsActive to false).
    /// </summary>
    Task<bool> DeleteTestCaseAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Updates a test case's name and description.
    /// </summary>
    Task<TestCaseResponse?> UpdateTestCaseAsync(int id, string name, string? description,
        CancellationToken ct = default);

    /// <summary>
    /// Gets test case runs for a specific execution.
    /// </summary>
    Task<List<TestCaseRunResponse>> GetRunsByExecutionAsync(string executionName, CancellationToken ct = default);

    /// <summary>
    /// Gets test case runs for a specific test case.
    /// </summary>
    Task<List<TestCaseRunResponse>> GetRunsByTestCaseAsync(int testCaseId, CancellationToken ct = default);

    /// <summary>
    /// Gets all test case runs.
    /// </summary>
    Task<List<TestCaseRunResponse>> GetAllRunsAsync(int limit = 500, CancellationToken ct = default);

    /// <summary>
    /// Gets stored test reports.
    /// </summary>
    Task<List<StoredReportResponse>> GetStoredReportsAsync(int limit = 100, CancellationToken ct = default);

    /// <summary>
    /// Deletes a stored report file but leaves the test run metrics intact.
    /// </summary>
    /// <param name="reportId">The ID of the report file to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the report was deleted, false if not found.</returns>
    Task<bool> DeleteReportFileAsync(int reportId, CancellationToken ct = default);
}

