using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace MattEland.Jaimes.Services.Services;

/// <summary>
/// Service for generating and managing test case HTML reports.
/// </summary>
public class TestCaseReportService(IDbContextFactory<JaimesDbContext> contextFactory) : ITestCaseReportService
{
    /// <inheritdoc/>
    public async Task<string> GenerateAndStoreReportAsync(string executionName, CancellationToken ct = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(ct);

        // Get all runs for this execution
        List<TestCaseRun> runs = await context.TestCaseRuns
            .Include(tcr => tcr.TestCase)
            .ThenInclude(tc => tc!.Message)
            .Include(tcr => tcr.Agent)
            .Include(tcr => tcr.InstructionVersion)
            .Include(tcr => tcr.Metrics)
            .ThenInclude(m => m.Evaluator)
            .Where(tcr => tcr.ExecutionName == executionName)
            .OrderBy(tcr => tcr.ExecutedAt)
            .ToListAsync(ct);

        if (runs.Count == 0)
        {
            throw new ArgumentException($"No test runs found for execution '{executionName}'", nameof(executionName));
        }

        // Generate HTML report
        string html = GenerateHtmlReport(executionName, runs);

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
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(ct);

        // Get all runs for all executions
        List<TestCaseRun> allRuns = await context.TestCaseRuns
            .Include(tcr => tcr.TestCase)
            .ThenInclude(tc => tc!.Message)
            .Include(tcr => tcr.Agent)
            .Include(tcr => tcr.InstructionVersion)
            .Include(tcr => tcr.Metrics)
            .ThenInclude(m => m.Evaluator)
            .Where(tcr => executionNames.Contains(tcr.ExecutionName ?? ""))
            .OrderBy(tcr => tcr.TestCaseId)
            .ThenBy(tcr => tcr.ExecutionName)
            .ToListAsync(ct);

        if (allRuns.Count == 0)
        {
            throw new ArgumentException("No test runs found for the provided execution names", nameof(executionNames));
        }

        return GenerateCombinedHtmlReport(executionNames, allRuns);
    }

    private static string GenerateCombinedHtmlReport(List<string> executionNames, List<TestCaseRun> allRuns)
    {
        var sb = new StringBuilder();

        // Group runs by test case and execution
        var runsByTestCase = allRuns.GroupBy(r => r.TestCaseId).OrderBy(g => g.Key).ToList();
        var versionsByExecution = allRuns
            .GroupBy(r => r.ExecutionName)
            .ToDictionary(g => g.Key ?? "", g => g.First());

        // Calculate aggregate stats
        var totalTestCases = runsByTestCase.Count;
        var totalVersions = versionsByExecution.Count;
        var avgDuration = allRuns.Where(r => r.DurationMs.HasValue).Select(r => r.DurationMs!.Value).DefaultIfEmpty(0)
            .Average();
        var allMetrics = allRuns.SelectMany(r => r.Metrics).ToList();
        var avgScore = allMetrics.Any() ? allMetrics.Average(m => m.Score) : 0;

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine("  <title>Combined Test Report</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine(
            "    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; line-height: 1.6; max-width: 1400px; margin: 0 auto; padding: 20px; background: #f5f5f5; }");
        sb.AppendLine("    h1 { color: #333; border-bottom: 2px solid #4CAF50; padding-bottom: 10px; }");
        sb.AppendLine(
            "    .summary { background: white; padding: 20px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); margin-bottom: 20px; }");
        sb.AppendLine("    .summary-stats { display: flex; gap: 30px; flex-wrap: wrap; }");
        sb.AppendLine("    .stat { text-align: center; }");
        sb.AppendLine("    .stat-value { font-size: 2em; font-weight: bold; color: #4CAF50; }");
        sb.AppendLine("    .stat-label { color: #666; font-size: 0.9em; }");
        sb.AppendLine(
            "    table { width: 100%; border-collapse: collapse; background: white; box-shadow: 0 2px 4px rgba(0,0,0,0.1); margin-top: 20px; }");
        sb.AppendLine("    th, td { padding: 12px; text-align: left; border-bottom: 1px solid #ddd; }");
        sb.AppendLine("    th { background: #4CAF50; color: white; font-weight: bold; position: sticky; top: 0; }");
        sb.AppendLine("    tr:hover { background: #f5f5f5; }");
        sb.AppendLine(
            "    .response { max-width: 300px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }");
        sb.AppendLine(
            "    .metric { display: inline-block; background: #f0f0f0; padding: 3px 8px; border-radius: 4px; margin: 2px; font-size: 0.85em; }");
        sb.AppendLine("    .metric-good { background: #c8e6c9; }");
        sb.AppendLine("    .metric-ok { background: #b3e5fc; }");
        sb.AppendLine("    .metric-warning { background: #fff9c4; }");
        sb.AppendLine("    .metric-poor { background: #e0e0e0; }");
        sb.AppendLine("    .metric-bad { background: #ffcdd2; }");
        sb.AppendLine("    .version-header { font-size: 0.9em; }");
        sb.AppendLine("    .timestamp { color: #999; font-size: 0.85em; }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        // Header
        sb.AppendLine("  <h1>📊 Combined Test Report</h1>");

        // Summary
        sb.AppendLine("  <div class=\"summary\">");
        sb.AppendLine("    <h2>Summary</h2>");
        sb.AppendLine("    <div class=\"summary-stats\">");
        sb.AppendLine(
            $"      <div class=\"stat\"><div class=\"stat-value\">{totalTestCases}</div><div class=\"stat-label\">Test Cases</div></div>");
        sb.AppendLine(
            $"      <div class=\"stat\"><div class=\"stat-value\">{totalVersions}</div><div class=\"stat-label\">Versions</div></div>");
        sb.AppendLine(
            $"      <div class=\"stat\"><div class=\"stat-value\">{avgDuration:F0}ms</div><div class=\"stat-label\">Avg Duration</div></div>");
        sb.AppendLine(
            $"      <div class=\"stat\"><div class=\"stat-value\">{avgScore:F2}</div><div class=\"stat-label\">Avg Score</div></div>");
        sb.AppendLine("    </div>");
        sb.AppendLine($"    <p class=\"timestamp\">Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>");
        sb.AppendLine("  </div>");

        // Comparison table
        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead>");
        sb.AppendLine("      <tr>");
        sb.AppendLine("        <th>Test Case</th>");

        foreach (var exec in versionsByExecution.Values)
        {
            sb.AppendLine(
                $"        <th class=\"version-header\">{HtmlEncode(exec.Agent?.Name ?? "Unknown")}<br/>{exec.InstructionVersion?.VersionNumber ?? "?"}</th>");
        }

        sb.AppendLine("      </tr>");
        sb.AppendLine("    </thead>");
        sb.AppendLine("    <tbody>");

        foreach (var testCaseGroup in runsByTestCase)
        {
            var testCaseName = testCaseGroup.First().TestCase?.Name ?? $"Test {testCaseGroup.Key}";
            sb.AppendLine("      <tr>");
            sb.AppendLine($"        <td><strong>{HtmlEncode(testCaseName)}</strong></td>");

            foreach (var execName in executionNames)
            {
                var run = testCaseGroup.FirstOrDefault(r => r.ExecutionName == execName);

                sb.AppendLine("        <td>");
                if (run != null)
                {
                    sb.AppendLine(
                        $"          <div class=\"response\" title=\"{HtmlEncode(run.GeneratedResponse)}\">{HtmlEncode(run.GeneratedResponse.Length > 100 ? run.GeneratedResponse.Substring(0, 100) + "..." : run.GeneratedResponse)}</div>");

                    if (run.Metrics.Any())
                    {
                        sb.AppendLine("          <div>");
                        foreach (var metric in run.Metrics)
                        {
                            var cssClass = metric.Score >= 5.0 ? "metric-good" :
                                metric.Score >= 4.0 ? "metric-ok" :
                                metric.Score >= 3.0 ? "metric-warning" :
                                metric.Score >= 2.0 ? "metric-poor" : "metric-bad";
                            sb.AppendLine(
                                $"            <span class=\"metric {cssClass}\" title=\"{HtmlEncode(metric.Evaluator?.Name ?? metric.MetricName)}\">{HtmlEncode(metric.MetricName.Substring(0, 1))}{metric.Score:F1}</span>");
                        }

                        sb.AppendLine("          </div>");
                    }

                    if (run.DurationMs.HasValue)
                    {
                        sb.AppendLine($"          <div class=\"timestamp\">{run.DurationMs}ms</div>");
                    }
                }
                else
                {
                    sb.AppendLine("          <span class=\"timestamp\">-</span>");
                }

                sb.AppendLine("        </td>");
            }

            sb.AppendLine("      </tr>");
        }

        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private static string GenerateHtmlReport(string executionName, List<TestCaseRun> runs)
    {
        var sb = new StringBuilder();

        // Calculate aggregate stats
        var totalRuns = runs.Count;
        var avgDuration = runs.Where(r => r.DurationMs.HasValue).Select(r => r.DurationMs!.Value).DefaultIfEmpty(0)
            .Average();
        var allMetrics = runs.SelectMany(r => r.Metrics).ToList();
        var avgScore = allMetrics.Any() ? allMetrics.Average(m => m.Score) : 0;

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"  <title>Test Report: {HtmlEncode(executionName)}</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine(
            "    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; line-height: 1.6; max-width: 1200px; margin: 0 auto; padding: 20px; background: #f5f5f5; }");
        sb.AppendLine("    h1 { color: #333; border-bottom: 2px solid #4CAF50; padding-bottom: 10px; }");
        sb.AppendLine("    h2 { color: #555; margin-top: 30px; }");
        sb.AppendLine(
            "    .summary { background: white; padding: 20px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); margin-bottom: 20px; }");
        sb.AppendLine("    .summary-stats { display: flex; gap: 30px; flex-wrap: wrap; }");
        sb.AppendLine("    .stat { text-align: center; }");
        sb.AppendLine("    .stat-value { font-size: 2em; font-weight: bold; color: #4CAF50; }");
        sb.AppendLine("    .stat-label { color: #666; font-size: 0.9em; }");
        sb.AppendLine(
            "    .test-case { background: white; padding: 20px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); margin-bottom: 15px; }");
        sb.AppendLine("    .test-case h3 { margin-top: 0; color: #333; }");
        sb.AppendLine(
            "    .message { background: #e3f2fd; padding: 15px; border-radius: 4px; margin: 10px 0; border-left: 4px solid #2196F3; }");
        sb.AppendLine(
            "    .response { background: #e8f5e9; padding: 15px; border-radius: 4px; margin: 10px 0; border-left: 4px solid #4CAF50; }");
        sb.AppendLine("    .metrics { margin-top: 15px; }");
        sb.AppendLine(
            "    .metric { display: inline-block; background: #f0f0f0; padding: 5px 10px; border-radius: 4px; margin: 5px 5px 5px 0; font-size: 0.9em; }");
        sb.AppendLine("    .metric-good { background: #c8e6c9; }");
        sb.AppendLine("    .metric-warning { background: #fff9c4; }");
        sb.AppendLine("    .metric-bad { background: #ffcdd2; }");
        sb.AppendLine("    .duration { color: #666; font-size: 0.9em; }");
        sb.AppendLine("    .timestamp { color: #999; font-size: 0.85em; }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        // Header
        sb.AppendLine($"  <h1>📊 Test Report: {HtmlEncode(executionName)}</h1>");

        // Summary section
        sb.AppendLine("  <div class=\"summary\">");
        sb.AppendLine("    <h2>Summary</h2>");
        sb.AppendLine("    <div class=\"summary-stats\">");
        sb.AppendLine(
            $"      <div class=\"stat\"><div class=\"stat-value\">{totalRuns}</div><div class=\"stat-label\">Test Cases</div></div>");
        sb.AppendLine(
            $"      <div class=\"stat\"><div class=\"stat-value\">{avgDuration:F0}ms</div><div class=\"stat-label\">Avg Duration</div></div>");
        sb.AppendLine(
            $"      <div class=\"stat\"><div class=\"stat-value\">{avgScore:F2}</div><div class=\"stat-label\">Avg Score</div></div>");
        sb.AppendLine("    </div>");

        if (runs.FirstOrDefault()?.Agent != null)
        {
            var agent = runs.First().Agent!;
            var version = runs.First().InstructionVersion;
            sb.AppendLine(
                $"    <p><strong>Agent:</strong> {HtmlEncode(agent.Name)} (Version {version?.VersionNumber ?? "?"})</p>");
        }

        sb.AppendLine($"    <p class=\"timestamp\">Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>");
        sb.AppendLine("  </div>");

        // Test case results
        sb.AppendLine("  <h2>Test Results</h2>");

        foreach (var run in runs)
        {
            var testCase = run.TestCase;
            var message = testCase?.Message;

            sb.AppendLine("  <div class=\"test-case\">");
            sb.AppendLine($"    <h3>🧪 {HtmlEncode(testCase?.Name ?? $"Test Case {run.TestCaseId}")}</h3>");

            if (message != null)
            {
                sb.AppendLine(
                    $"    <div class=\"message\"><strong>User Message:</strong> {HtmlEncode(message.Text ?? "")}</div>");
            }

            sb.AppendLine(
                $"    <div class=\"response\"><strong>Agent Response:</strong> {HtmlEncode(run.GeneratedResponse)}</div>");

            if (run.DurationMs.HasValue)
            {
                sb.AppendLine($"    <p class=\"duration\">⏱️ Duration: {run.DurationMs}ms</p>");
            }

            // Metrics
            if (run.Metrics.Any())
            {
                sb.AppendLine("    <div class=\"metrics\">");
                foreach (var metric in run.Metrics)
                {
                    var cssClass = metric.Score >= 0.7 ? "metric-good" :
                        metric.Score >= 0.4 ? "metric-warning" : "metric-bad";
                    var evaluatorName = metric.Evaluator?.Name ?? metric.MetricName;
                    sb.AppendLine(
                        $"      <span class=\"metric {cssClass}\">{HtmlEncode(evaluatorName)}: {metric.Score:F2}</span>");
                }

                sb.AppendLine("    </div>");
            }

            sb.AppendLine($"    <p class=\"timestamp\">Executed: {run.ExecutedAt:yyyy-MM-dd HH:mm:ss} UTC</p>");
            sb.AppendLine("  </div>");
        }

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private static string HtmlEncode(string text)
    {
        return System.Net.WebUtility.HtmlEncode(text);
    }
}
