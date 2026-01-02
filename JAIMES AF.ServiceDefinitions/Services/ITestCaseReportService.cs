namespace MattEland.Jaimes.ServiceDefinitions.Services;

/// <summary>
/// Service for generating and managing test case reports.
/// </summary>
public interface ITestCaseReportService
{
    /// <summary>
    /// Generates an HTML report for a test execution and stores it in the database.
    /// </summary>
    /// <param name="executionName">The name of the test execution.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The generated HTML report content.</returns>
    Task<string> GenerateAndStoreReportAsync(string executionName, CancellationToken ct = default);

    /// <summary>
    /// Gets the stored report for a test execution.
    /// </summary>
    /// <param name="executionName">The name of the test execution.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The HTML report content, or null if not found.</returns>
    Task<string?> GetStoredReportAsync(string executionName, CancellationToken ct = default);

    /// <summary>
    /// Generates a combined HTML report for multiple test executions (multi-version runs).
    /// </summary>
    /// <param name="executionNames">List of execution names to include in the report</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The generated HTML report as a string</returns>
    Task<string> GenerateCombinedReportAsync(List<string> executionNames, CancellationToken ct = default);
}
