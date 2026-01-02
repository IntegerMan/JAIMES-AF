using System.Diagnostics;
using MattEland.Jaimes.Agents.Helpers;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace MattEland.Jaimes.ApiService.Services;

/// <summary>
/// Service for running agents against test cases in isolation.
/// Messages generated during testing do not go through the standard messaging pipeline.
/// </summary>
public class AgentTestRunner(
    IDbContextFactory<JaimesDbContext> contextFactory,
    IChatClient chatClient,
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
}
