using System.Diagnostics;
using System.Text;
using System.Text.Json;
using MattEland.Jaimes.Agents.Helpers;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Reporting;
using Microsoft.Extensions.AI.Evaluation.Reporting.Formats.Html;
using Microsoft.Extensions.Logging;

namespace MattEland.Jaimes.ApiService.Services;

/// <summary>
/// Service for running agents against test cases in isolation.
/// Messages generated during testing do not go through the standard messaging pipeline.
/// </summary>
public class AgentTestRunner(
    IDbContextFactory<JaimesDbContext> contextFactory,
    IChatClient chatClient,
    IEnumerable<IEvaluator> evaluators,
    IEvaluationResultStore resultStore,
    ILogger<AgentTestRunner> logger) : IAgentTestRunner
{
    private const int MaxContextMessages = 5;

    /// <inheritdoc/>
    public async Task<TestRunResultResponse> RunTestCasesAsync(
        string agentId,
        int instructionVersionId,
        IEnumerable<int>? testCaseIds = null,
        string? executionName = null,
        CancellationToken ct = default)
    {
        DateTime startedAt = DateTime.UtcNow;
        executionName ??= $"test-run-{agentId}-{instructionVersionId}-{startedAt:yyyyMMddHHmmss}";

        logger.LogInformation("Starting test run {ExecutionName} for agent {AgentId} version {VersionId}",
            executionName, agentId, instructionVersionId);

        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(ct);

        // Verify agent and version exist
        Agent? agent = await context.Agents.FindAsync([agentId], ct);
        if (agent == null)
        {
            throw new InvalidOperationException($"Agent {agentId} not found.");
        }

        AgentInstructionVersion? version = await context.AgentInstructionVersions
            .FirstOrDefaultAsync(v => v.Id == instructionVersionId && v.AgentId == agentId, ct);
        if (version == null)
        {
            throw new InvalidOperationException(
                $"Instruction version {instructionVersionId} not found for agent {agentId}.");
        }

        // Get test cases to run
        List<int> testCaseIdList = testCaseIds?.ToList() ?? [];
        IQueryable<TestCase> testCaseQuery = context.TestCases
            .Include(tc => tc.Message)
            .Where(tc => tc.IsActive);

        if (testCaseIdList.Count > 0)
        {
            testCaseQuery = testCaseQuery.Where(tc => testCaseIdList.Contains(tc.Id));
        }

        List<TestCase> testCases = await testCaseQuery.ToListAsync(ct);
        if (testCases.Count == 0)
        {
            throw new InvalidOperationException("No test cases found to run.");
        }

        logger.LogInformation("Running {TestCaseCount} test cases", testCases.Count);

        List<TestCaseRunResponse> runResponses = [];
        int failedCount = 0;

        foreach (TestCase testCase in testCases)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                TestCaseRunResponse runResponse = await RunSingleTestCaseAsync(
                    context, testCase, agent, version, executionName, ct);
                runResponses.Add(runResponse);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to run test case {TestCaseId}: {Name}", testCase.Id, testCase.Name);
                failedCount++;
            }
        }

        // Calculate average score
        double? averageScore = null;
        List<double> allScores = runResponses
            .SelectMany(r => r.Metrics)
            .Select(m => m.Score)
            .ToList();
        if (allScores.Count > 0)
        {
            averageScore = allScores.Average();
        }

        return new TestRunResultResponse
        {
            ExecutionName = executionName,
            AgentId = agentId,
            AgentName = agent.Name,
            InstructionVersionId = instructionVersionId,
            VersionNumber = version.VersionNumber,
            StartedAt = startedAt,
            CompletedAt = DateTime.UtcNow,
            TotalTestCases = testCases.Count,
            CompletedTestCases = runResponses.Count,
            FailedTestCases = failedCount,
            AverageScore = averageScore,
            Runs = runResponses
        };
    }

    private async Task<TestCaseRunResponse> RunSingleTestCaseAsync(
        JaimesDbContext context,
        TestCase testCase,
        Agent agent,
        AgentInstructionVersion version,
        string executionName,
        CancellationToken ct)
    {
        logger.LogInformation("Running test case {TestCaseId}: {Name}", testCase.Id, testCase.Name);

        // Get the last N messages before the test case message for context
        List<ChatMessage> chatMessages = await BuildTestContextAsync(context, testCase.Message!, ct);

        // Create an isolated agent with the version's instructions
        AIAgent testAgent = chatClient.CreateJaimesAgent(
            logger,
            $"TestAgent-{agent.Id}-{version.Id}",
            version.Instructions,
            tools: null); // No tools for test runs to keep them isolated

        // Run the agent
        Stopwatch sw = Stopwatch.StartNew();
        AgentRunResponse response = await testAgent.RunAsync(chatMessages, null, null, ct);
        sw.Stop();

        // Extract the generated response text
        string generatedResponse = string.Join("\n",
            response.Messages
                .Where(m => m.Role == ChatRole.Assistant)
                .Select(m => m.Text)
                .Where(t => !string.IsNullOrEmpty(t)));

        logger.LogInformation("Test case {TestCaseId} completed in {DurationMs}ms, response length: {Length}",
            testCase.Id, sw.ElapsedMilliseconds, generatedResponse.Length);

        // Create and save the run record
        TestCaseRun run = new()
        {
            TestCaseId = testCase.Id,
            AgentId = agent.Id,
            InstructionVersionId = version.Id,
            ExecutedAt = DateTime.UtcNow,
            GeneratedResponse = generatedResponse,
            DurationMs = (int)sw.ElapsedMilliseconds,
            ExecutionName = executionName
        };

        context.TestCaseRuns.Add(run);
        await context.SaveChangesAsync(ct);

        // Evaluate the response
        await EvaluateTestRunAsync(context, run, testCase, chatMessages, generatedResponse, agent, version, ct);

        return new TestCaseRunResponse
        {
            Id = run.Id,
            TestCaseId = testCase.Id,
            TestCaseName = testCase.Name,
            AgentId = agent.Id,
            AgentName = agent.Name,
            InstructionVersionId = version.Id,
            VersionNumber = version.VersionNumber,
            ExecutedAt = run.ExecutedAt,
            GeneratedResponse = generatedResponse,
            DurationMs = run.DurationMs,
            ExecutionName = executionName,
            Metrics = [] // Metrics would be populated by a separate evaluation pass
        };
    }

    private async Task<List<ChatMessage>> BuildTestContextAsync(
        JaimesDbContext context,
        Message testCaseMessage,
        CancellationToken ct)
    {
        // Get the last N messages before and including the test case message
        // Using the linked list structure to traverse backwards
        List<Message> messagesForContext = [];
        Message? currentMessage = testCaseMessage;

        // Add the test case message (player message)
        messagesForContext.Add(currentMessage);

        // Traverse backwards through the linked list
        int count = 0;
        while (count < MaxContextMessages && currentMessage.PreviousMessageId.HasValue)
        {
            Message? prevMessage = await context.Messages
                .FirstOrDefaultAsync(m => m.Id == currentMessage.PreviousMessageId.Value, ct);

            if (prevMessage == null) break;

            messagesForContext.Insert(0, prevMessage);
            currentMessage = prevMessage;
            count++;
        }

        // Convert to ChatMessage format
        return messagesForContext
            .Select(m => new ChatMessage(
                string.IsNullOrEmpty(m.PlayerId) ? ChatRole.Assistant : ChatRole.User,
                m.Text))
            .ToList();
    }

    private async Task EvaluateTestRunAsync(
        JaimesDbContext context,
        TestCaseRun run,
        TestCase testCase,
        List<ChatMessage> contextMessages,
        string generatedResponse,
        Agent agent,
        AgentInstructionVersion version,
        CancellationToken ct)
    {
        try
        {
            logger.LogInformation("Evaluating test run {RunId}", run.Id);

            // Create the assistant response
            ChatMessage assistantMessage = new(ChatRole.Assistant, generatedResponse);
            ChatResponse assistantResponse = new(assistantMessage);

            // Create evaluation context
            List<ChatMessage> evaluationContext = [new ChatMessage(ChatRole.System, version.Instructions)];
            evaluationContext.AddRange(contextMessages);

            // Create composite evaluator
            IEvaluator compositeEvaluator = new CompositeEvaluator(evaluators);

            // Create ReportingConfiguration
            ChatConfiguration chatConfiguration = new(chatClient);
            ReportingConfiguration reportConfig = new(
                [compositeEvaluator],
                resultStore,
                executionName: $"TestRun_{run.ExecutionName}",
                chatConfiguration: chatConfiguration);

            // Create scenario run - scenario is the test case, iteration is the agent version
            // This allows comparing different agent versions against the same test case
            await using ScenarioRun scenarioRun = await reportConfig.CreateScenarioRunAsync(
                scenarioName: $"TC{testCase.Id}: {testCase.Name}",
                iterationName: $"{agent.Name} {version.VersionNumber} [Agent:{agent.Id}|Version:{version.Id}]",
                cancellationToken: ct);

            // Perform evaluation
            EvaluationResult result = await scenarioRun.EvaluateAsync(
                evaluationContext,
                assistantResponse,
                cancellationToken: ct);

            // Load evaluator lookups
            var evaluatorIdLookup = await context.Evaluators
                .ToDictionaryAsync(e => e.Name.ToLower(), e => e.Id, ct);

            var metricToEvaluatorMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var evaluator in evaluators)
            {
                string className = evaluator.GetType().Name;
                foreach (var metricName in evaluator.EvaluationMetricNames)
                {
                    metricToEvaluatorMap[metricName] = className;
                }
            }

            // Store metrics with diagnostics
            DateTime evaluatedAt = DateTime.UtcNow;
            foreach (var metricPair in result.Metrics)
            {
                if (metricPair.Value is NumericMetric metric && metric.Value != null)
                {
                    int? evaluatorId = null;
                    if (metricToEvaluatorMap.TryGetValue(metricPair.Key, out string? evaluatorClassName))
                    {
                        if (evaluatorIdLookup.TryGetValue(evaluatorClassName.ToLower(), out int id))
                        {
                            evaluatorId = id;
                        }
                    }

                    // Serialize diagnostics
                    string? diagnosticsJson = null;
                    if (metric.Diagnostics?.Any() == true)
                    {
                        var diagnostics = metric.Diagnostics.Select(d => d.Message).ToList();
                        diagnosticsJson = JsonSerializer.Serialize(diagnostics);
                    }

                    context.TestCaseRunMetrics.Add(new TestCaseRunMetric
                    {
                        TestCaseRunId = run.Id,
                        MetricName = metricPair.Key,
                        Score = metric.Value.Value,
                        Remarks = metric.Reason,
                        EvaluatorId = evaluatorId,
                        EvaluatedAt = evaluatedAt,
                        Diagnostics = diagnosticsJson
                    });
                }
            }

            await context.SaveChangesAsync(ct);
            logger.LogInformation("Evaluation complete for run {RunId}: {MetricCount} metrics", run.Id,
                result.Metrics.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Evaluation failed for run {RunId}", run.Id);
        }
    }

    /// <inheritdoc/>
    public async Task<MultiVersionTestRunResponse> RunMultiVersionTestsAsync(
        IEnumerable<VersionToTest> versions,
        IEnumerable<int>? testCaseIds = null,
        string? executionName = null,
        IEnumerable<string>? evaluatorNames = null,
        CancellationToken ct = default)
    {
        DateTime startedAt = DateTime.UtcNow;
        executionName ??= $"multi-test-{startedAt:yyyyMMddHHmmss}";
        List<VersionToTest> versionList = versions.ToList();

        logger.LogInformation(
            "Starting multi-version test run {ExecutionName} for {VersionCount} versions",
            executionName, versionList.Count);

        if (versionList.Count == 0)
        {
            throw new ArgumentException("At least one version is required", nameof(versions));
        }

        List<TestRunResultResponse> versionResults = [];
        int totalRuns = 0;
        int completedRuns = 0;
        int failedRuns = 0;

        // Run tests for each version, all under the same execution name
        foreach (VersionToTest versionToTest in versionList)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                TestRunResultResponse result = await RunTestCasesAsync(
                    versionToTest.AgentId,
                    versionToTest.InstructionVersionId,
                    testCaseIds,
                    executionName,
                    ct);

                versionResults.Add(result);
                totalRuns += result.TotalTestCases;
                completedRuns += result.CompletedTestCases;
                failedRuns += result.FailedTestCases;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to run tests for agent {AgentId} version {VersionId}",
                    versionToTest.AgentId, versionToTest.InstructionVersionId);
                failedRuns++;
            }
        }

        // Calculate overall average score
        double? averageScore = null;
        List<double> allScores = versionResults
            .SelectMany(vr => vr.Runs)
            .SelectMany(r => r.Metrics)
            .Select(m => m.Score)
            .ToList();
        if (allScores.Count > 0)
        {
            averageScore = allScores.Average();
        }

        // Generate and store the combined report
        int? reportFileId = await GenerateAndStoreReportAsync(executionName, ct);

        return new MultiVersionTestRunResponse
        {
            ExecutionName = executionName,
            StartedAt = startedAt,
            CompletedAt = DateTime.UtcNow,
            TotalRuns = totalRuns,
            CompletedRuns = completedRuns,
            FailedRuns = failedRuns,
            AverageScore = averageScore,
            VersionResults = versionResults,
            ReportFileId = reportFileId
        };
    }

    private async Task<int?> GenerateAndStoreReportAsync(string executionName, CancellationToken ct)
    {
        try
        {
            await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(ct);

            // Read results from evaluation store
            string storeExecutionName = $"TestRun_{executionName}";
            var results = new List<ScenarioRunResult>();

            await foreach (var result in resultStore.ReadResultsAsync(
                               executionName: storeExecutionName,
                               cancellationToken: ct))
            {
                results.Add(result);
            }

            if (results.Count == 0)
            {
                logger.LogWarning("No evaluation results found for {ExecutionName}", executionName);
                return null;
            }

            // Generate HTML report using HtmlReportWriter
            string tempPath = Path.Combine(Path.GetTempPath(), $"report_{Guid.NewGuid()}.html");
            string html;

            try
            {
                var writer = new HtmlReportWriter(tempPath);
                await writer.WriteReportAsync(results, ct);
                html = await File.ReadAllTextAsync(tempPath, ct);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }

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
            var runs = await context.TestCaseRuns
                .Where(tcr => tcr.ExecutionName == executionName)
                .ToListAsync(ct);

            foreach (var run in runs)
            {
                run.ReportFileId = storedFile.Id;
            }

            await context.SaveChangesAsync(ct);

            logger.LogInformation("Generated and stored report {FileId} for {ExecutionName}",
                storedFile.Id, executionName);

            return storedFile.Id;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate report for {ExecutionName}", executionName);
            return null;
        }
    }
}
