using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.EntityFrameworkCore;

namespace MattEland.Jaimes.Services.Services;

/// <summary>
/// Service for managing test cases.
/// </summary>
public class TestCaseService(IDbContextFactory<JaimesDbContext> contextFactory) : ITestCaseService
{
    /// <inheritdoc/>
    public async Task<TestCaseResponse?> GetTestCaseAsync(int id, CancellationToken ct = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(ct);

        TestCase? testCase = await context.TestCases
            .Include(tc => tc.Message)
            .ThenInclude(m => m!.Game)
            .Include(tc => tc.Message)
            .ThenInclude(m => m!.Agent)
            .Include(tc => tc.Runs)
            .AsNoTracking()
            .FirstOrDefaultAsync(tc => tc.Id == id, ct);

        return testCase == null ? null : MapToResponse(testCase);
    }

    /// <inheritdoc/>
    public async Task<TestCaseResponse?> GetTestCaseByMessageIdAsync(int messageId, CancellationToken ct = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(ct);

        TestCase? testCase = await context.TestCases
            .Include(tc => tc.Message)
            .ThenInclude(m => m!.Game)
            .Include(tc => tc.Message)
            .ThenInclude(m => m!.Agent)
            .Include(tc => tc.Runs)
            .AsNoTracking()
            .FirstOrDefaultAsync(tc => tc.MessageId == messageId, ct);

        return testCase == null ? null : MapToResponse(testCase);
    }

    /// <inheritdoc/>
    public async Task<List<TestCaseResponse>> ListTestCasesAsync(string? agentId = null, bool includeInactive = false,
        CancellationToken ct = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(ct);

        IQueryable<TestCase> query = context.TestCases
            .Include(tc => tc.Message)
            .ThenInclude(m => m!.Game)
            .Include(tc => tc.Message)
            .ThenInclude(m => m!.Agent)
            .Include(tc => tc.Runs)
            .AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(tc => tc.IsActive);
        }

        if (!string.IsNullOrEmpty(agentId))
        {
            query = query.Where(tc => tc.Message!.AgentId == agentId);
        }

        List<TestCase> testCases = await query
            .OrderByDescending(tc => tc.CreatedAt)
            .ToListAsync(ct);

        return testCases.Select(MapToResponse).ToList();
    }

    /// <inheritdoc/>
    public async Task<TestCaseResponse> CreateTestCaseAsync(int messageId, string name, string? description,
        CancellationToken ct = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(ct);

        // Verify the message exists and is a player message
        Message? message = await context.Messages
            .Include(m => m.Game)
            .Include(m => m.Agent)
            .FirstOrDefaultAsync(m => m.Id == messageId, ct);

        if (message == null)
        {
            throw new InvalidOperationException($"Message with ID {messageId} not found.");
        }

        if (string.IsNullOrEmpty(message.PlayerId))
        {
            throw new InvalidOperationException("Only player messages can be marked as test cases.");
        }

        // Check if test case already exists for this message
        bool exists = await context.TestCases.AnyAsync(tc => tc.MessageId == messageId, ct);
        if (exists)
        {
            throw new InvalidOperationException($"A test case already exists for message {messageId}.");
        }

        TestCase testCase = new()
        {
            MessageId = messageId,
            Name = name,
            Description = description,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        context.TestCases.Add(testCase);
        await context.SaveChangesAsync(ct);

        // Reload with includes
        testCase = await context.TestCases
            .Include(tc => tc.Message)
            .ThenInclude(m => m!.Game)
            .Include(tc => tc.Message)
            .ThenInclude(m => m!.Agent)
            .Include(tc => tc.Runs)
            .FirstAsync(tc => tc.Id == testCase.Id, ct);

        return MapToResponse(testCase);
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteTestCaseAsync(int id, CancellationToken ct = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(ct);

        TestCase? testCase = await context.TestCases.FindAsync([id], ct);
        if (testCase == null)
        {
            return false;
        }

        testCase.IsActive = false;
        await context.SaveChangesAsync(ct);
        return true;
    }

    /// <inheritdoc/>
    public async Task<TestCaseResponse?> UpdateTestCaseAsync(int id, string name, string? description,
        CancellationToken ct = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(ct);

        TestCase? testCase = await context.TestCases
            .Include(tc => tc.Message)
            .ThenInclude(m => m!.Game)
            .Include(tc => tc.Message)
            .ThenInclude(m => m!.Agent)
            .Include(tc => tc.Runs)
            .FirstOrDefaultAsync(tc => tc.Id == id, ct);

        if (testCase == null)
        {
            return null;
        }

        testCase.Name = name;
        testCase.Description = description;
        await context.SaveChangesAsync(ct);

        return MapToResponse(testCase);
    }

    /// <inheritdoc/>
    public async Task<List<TestCaseRunResponse>> GetRunsByExecutionAsync(string executionName,
        CancellationToken ct = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(ct);

        List<TestCaseRun> runs = await context.TestCaseRuns
            .Include(tcr => tcr.TestCase)
            .Include(tcr => tcr.Agent)
            .Include(tcr => tcr.InstructionVersion)
            .Include(tcr => tcr.Metrics)
            .ThenInclude(m => m.Evaluator)
            .AsNoTracking()
            .Where(tcr => tcr.ExecutionName == executionName)
            .OrderBy(tcr => tcr.ExecutedAt)
            .ToListAsync(ct);

        return runs.Select(MapRunToResponse).ToList();
    }

    /// <inheritdoc/>
    public async Task<List<TestCaseRunResponse>> GetRunsByTestCaseAsync(int testCaseId, CancellationToken ct = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(ct);

        List<TestCaseRun> runs = await context.TestCaseRuns
            .Include(tcr => tcr.TestCase)
            .Include(tcr => tcr.Agent)
            .Include(tcr => tcr.InstructionVersion)
            .Include(tcr => tcr.Metrics)
            .ThenInclude(m => m.Evaluator)
            .AsNoTracking()
            .Where(tcr => tcr.TestCaseId == testCaseId)
            .OrderByDescending(tcr => tcr.ExecutedAt)
            .ToListAsync(ct);

        return runs.Select(MapRunToResponse).ToList();
    }

    /// <inheritdoc/>
    public async Task<List<TestCaseRunResponse>> GetAllRunsAsync(int limit = 500, CancellationToken ct = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(ct);

        List<TestCaseRun> runs = await context.TestCaseRuns
            .Include(tcr => tcr.TestCase)
            .Include(tcr => tcr.Agent)
            .Include(tcr => tcr.InstructionVersion)
            .Include(tcr => tcr.Metrics)
            .ThenInclude(m => m.Evaluator)
            .AsNoTracking()
            .OrderByDescending(tcr => tcr.ExecutedAt)
            .Take(limit)
            .ToListAsync(ct);

        return runs.Select(MapRunToResponse).ToList();
    }

    /// <inheritdoc/>
    public async Task<List<StoredReportResponse>> GetStoredReportsAsync(int limit = 100, CancellationToken ct = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(ct);

        // Query runs that have stored reports, including metrics for evaluator count
        var runsWithReports = await context.TestCaseRuns
            .Include(tcr => tcr.ReportFile)
            .Include(tcr => tcr.Agent)
            .Include(tcr => tcr.InstructionVersion)
            .Include(tcr => tcr.Metrics)
            .AsNoTracking()
            .Where(tcr => tcr.ReportFileId != null)
            .OrderByDescending(tcr => tcr.ReportFile!.CreatedAt)
            .ToListAsync(ct);

        // Group by report file to get unique reports
        var reports = runsWithReports
            .Where(r => r.ExecutionName != null)
            .GroupBy(r => r.ReportFileId)
            .Select(g =>
            {
                var firstRun = g.First();
                var isDeleted = firstRun.ReportFile == null;

                // Collect all unique agent/version combinations
                var agentVersions = g
                    .GroupBy(r => new { r.AgentId, r.InstructionVersionId })
                    .Select(av =>
                    {
                        var run = av.First();
                        return new ReportAgentVersionInfo
                        {
                            AgentId = run.AgentId,
                            AgentName = run.Agent?.Name ?? run.AgentId,
                            InstructionVersionId = run.InstructionVersionId,
                            VersionNumber = run.InstructionVersion?.VersionNumber ?? "?",
                            TestCaseCount = av.Count()
                        };
                    })
                    .OrderBy(av => av.AgentName)
                    .ThenBy(av => av.VersionNumber)
                    .ToList();

                // Count unique evaluators from all metrics
                var evaluatorCount = g
                    .SelectMany(r => r.Metrics ?? [])
                    .Where(m => m.EvaluatorId.HasValue)
                    .Select(m => m.EvaluatorId!.Value)
                    .Distinct()
                    .Count();

                return new StoredReportResponse
                {
                    ReportId = firstRun.ReportFileId ?? 0,
                    FileName = firstRun.ReportFile?.FileName ?? "(deleted)",
                    CreatedAt = firstRun.ReportFile?.CreatedAt ?? firstRun.ExecutedAt,
                    SizeBytes = firstRun.ReportFile?.SizeBytes,
                    ExecutionName = firstRun.ExecutionName!,
                    AgentVersions = agentVersions,
                    TotalTestCaseCount = g.Count(),
                    EvaluatorCount = evaluatorCount,
                    IsDeleted = isDeleted
                };
            })
            .Take(limit)
            .ToList();

        return reports;
    }


    private static TestCaseResponse MapToResponse(TestCase testCase) => new()
    {
        Id = testCase.Id,
        MessageId = testCase.MessageId,
        Name = testCase.Name,
        Description = testCase.Description,
        CreatedAt = testCase.CreatedAt,
        IsActive = testCase.IsActive,
        GameId = testCase.Message?.GameId ?? Guid.Empty,
        GameTitle = testCase.Message?.Game?.Title,
        MessageText = testCase.Message?.Text,
        AgentId = testCase.Message?.AgentId,
        AgentName = testCase.Message?.Agent?.Name,
        RunCount = testCase.Runs?.Count ?? 0
    };

    private static TestCaseRunResponse MapRunToResponse(TestCaseRun run) => new()
    {
        Id = run.Id,
        TestCaseId = run.TestCaseId,
        TestCaseName = run.TestCase?.Name ?? "",
        AgentId = run.AgentId,
        AgentName = run.Agent?.Name,
        InstructionVersionId = run.InstructionVersionId,
        VersionNumber = run.InstructionVersion?.VersionNumber,
        ExecutedAt = run.ExecutedAt,
        GeneratedResponse = run.GeneratedResponse,
        DurationMs = run.DurationMs,
        ExecutionName = run.ExecutionName,
        Metrics = run.Metrics?.Select(m => new TestCaseRunMetricResponse
        {
            Id = m.Id,
            MetricName = m.MetricName,
            Score = m.Score,
            Remarks = m.Remarks,
            EvaluatorId = m.EvaluatorId,
            EvaluatorName = m.Evaluator?.Name
        }).ToList() ?? []
    };

    /// <inheritdoc/>
    public async Task<bool> DeleteReportFileAsync(int reportId, CancellationToken ct = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(ct);

        // Find the stored file
        var storedFile = await context.StoredFiles.FindAsync([reportId], ct);
        if (storedFile == null)
        {
            return false;
        }

        // Clear references from test case runs (keeps the runs but removes file link)
        var runsWithReport = await context.TestCaseRuns
            .Where(tcr => tcr.ReportFileId == reportId)
            .ToListAsync(ct);

        foreach (var run in runsWithReport)
        {
            run.ReportFileId = null;
        }

        // Delete the stored file
        context.StoredFiles.Remove(storedFile);
        await context.SaveChangesAsync(ct);

        return true;
    }
}

