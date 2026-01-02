using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI.Evaluation.Reporting;
using Microsoft.Extensions.AI.Evaluation.Reporting.Formats.Html;
using System.Text;

namespace MattEland.Jaimes.Services.Services;

/// <summary>
/// Service for generating and managing test case HTML reports using HtmlReportWriter.
/// </summary>
public class TestCaseReportService(
    IDbContextFactory<JaimesDbContext> contextFactory,
    IEvaluationResultStore resultStore) : ITestCaseReportService
{
    /// <inheritdoc/>
    public async Task<string> GenerateAndStoreReportAsync(string executionName, CancellationToken ct = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(ct);

        // Get runs for this execution to validate and link report
        List<TestCaseRun> runs = await context.TestCaseRuns
            .Where(tcr => tcr.ExecutionName == executionName)
            .ToListAsync(ct);

        if (runs.Count == 0)
        {
            throw new ArgumentException($"No test runs found for execution '{executionName}'", nameof(executionName));
        }

        // Read results from evaluation store and generate HTML report
        string html = await GenerateHtmlFromStoreAsync($"TestRun_{executionName}", ct);

        // Store the report
        var storedFile = new StoredFile
        {
            ItemKind = "TestReport",
            FileName = $"{executionName}-report.html",
            ContentType = "text/html",
            Content = html,
            CreatedAt = DateTime.UtcNow,
            SizeBytes = Encoding.UTF8.GetByteCount(html)
        };

        context.StoredFiles.Add(storedFile);
        await context.SaveChangesAsync(ct);

        // Link the report to all runs in this execution
        foreach (var run in runs)
        {
            run.ReportFileId = storedFile.Id;
        }

        await context.SaveChangesAsync(ct);

        return html;
    }

    /// <inheritdoc/>
    public async Task<string?> GetStoredReportAsync(string executionName, CancellationToken ct = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(ct);

        // Find the first run with this execution name that has a report
        var run = await context.TestCaseRuns
            .Include(tcr => tcr.ReportFile)
            .Where(tcr => tcr.ExecutionName == executionName && tcr.ReportFileId != null)
            .FirstOrDefaultAsync(ct);

        return run?.ReportFile?.Content;
    }

    /// <inheritdoc/>
    public async Task<string> GenerateCombinedReportAsync(List<string> executionNames, CancellationToken ct = default)
    {
        if (executionNames.Count == 0)
        {
            throw new ArgumentException("At least one execution name is required", nameof(executionNames));
        }

        // Collect all results from the evaluation store for the given execution names
        var allResults = new List<ScenarioRunResult>();

        foreach (var execName in executionNames)
        {
            // The AgentTestRunner uses: $"TestRun_{executionName}" for the ReportingConfiguration executionName
            string storeExecutionName = $"TestRun_{execName}";

            await foreach (var result in resultStore.ReadResultsAsync(
                               executionName: storeExecutionName,
                               cancellationToken: ct))
            {
                allResults.Add(result);
            }
        }

        if (allResults.Count == 0)
        {
            throw new ArgumentException("No evaluation results found for the provided execution names",
                nameof(executionNames));
        }

        // Generate HTML report using HtmlReportWriter
        return await GenerateHtmlReportAsync(allResults, ct);
    }

    private async Task<string> GenerateHtmlFromStoreAsync(string executionName, CancellationToken ct)
    {
        var results = new List<ScenarioRunResult>();

        await foreach (var result in resultStore.ReadResultsAsync(
                           executionName: executionName,
                           cancellationToken: ct))
        {
            results.Add(result);
        }

        if (results.Count == 0)
        {
            // Fall back to empty report if no evaluation results yet
            return GenerateFallbackHtml(executionName);
        }

        return await GenerateHtmlReportAsync(results, ct);
    }

    private static async Task<string> GenerateHtmlReportAsync(IEnumerable<ScenarioRunResult> results,
        CancellationToken ct)
    {
        // Write to temp file and read back (HtmlReportWriter writes to file path)
        string tempPath = Path.Combine(Path.GetTempPath(), $"report_{Guid.NewGuid()}.html");

        try
        {
            var writer = new HtmlReportWriter(tempPath);
            await writer.WriteReportAsync(results, ct);
            return await File.ReadAllTextAsync(tempPath, ct);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static string GenerateFallbackHtml(string executionName)
    {
        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <title>Test Report: {executionName}</title>
</head>
<body>
    <h1>Test Report: {executionName}</h1>
    <p>No evaluation results available yet. Run evaluations to generate detailed metrics.</p>
</body>
</html>";
    }
}
