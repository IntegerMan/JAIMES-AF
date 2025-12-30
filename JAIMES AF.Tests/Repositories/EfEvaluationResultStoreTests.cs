using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Reporting;
using Moq;
using Shouldly;

namespace MattEland.Jaimes.Tests.Repositories;

public class EfEvaluationResultStoreTests : IAsyncLifetime
{
    private JaimesDbContext _context = null!;
    private EfEvaluationResultStore _store = null!;
    private DbContextOptions<JaimesDbContext> _options = null!;

    public async ValueTask InitializeAsync()
    {
        _options = new DbContextOptionsBuilder<JaimesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new JaimesDbContext(_options);
        await _context.Database.EnsureCreatedAsync();

        _store = new EfEvaluationResultStore(new TestDbContextFactory(_options));
    }

    private class TestDbContextFactory(DbContextOptions<JaimesDbContext> options) : IDbContextFactory<JaimesDbContext>
    {
        public JaimesDbContext CreateDbContext() => new(options);
    }

    public async ValueTask DisposeAsync()
    {
        if (_context != null)
        {
            await _context.DisposeAsync();
        }
    }

    [Fact]
    public async Task WriteResultsAsync_ShouldPersistResults()
    {
        // Arrange
        string executionName = "TestExecution";
        ScenarioRunResult result = CreateTestResult(executionName, "TestScenario", "TestIteration");
        List<ScenarioRunResult> results = [result];

        // Act
        await _store.WriteResultsAsync(results, TestContext.Current.CancellationToken);

        // Assert
        using JaimesDbContext context = new(_options);
        EvaluationExecution? execution =
            await context.EvaluationExecutions.FirstOrDefaultAsync(e => e.ExecutionName == executionName,
                TestContext.Current.CancellationToken);
        execution.ShouldNotBeNull();

        EvaluationScenarioIteration? iteration = await context.EvaluationScenarioIterations
            .FirstOrDefaultAsync(si => si.ExecutionName == executionName, TestContext.Current.CancellationToken);
        iteration.ShouldNotBeNull();
        iteration.ScenarioName.ShouldBe("TestScenario");
        iteration.IterationName.ShouldBe("TestIteration");
    }

    [Fact]
    public async Task ReadResultsAsync_ShouldReturnPersistedResults()
    {
        // Arrange
        string executionName = "TestExecution";
        ScenarioRunResult result = CreateTestResult(executionName, "TestScenario", "TestIteration");
        await _store.WriteResultsAsync([result], TestContext.Current.CancellationToken);

        // Act
        List<ScenarioRunResult> results = new();
        await foreach (var r in _store.ReadResultsAsync(executionName,
                           cancellationToken: TestContext.Current.CancellationToken))
        {
            results.Add(r);
        }

        // Assert
        results.Count.ShouldBe(1);
        results[0].ExecutionName.ShouldBe(executionName);
        results[0].ScenarioName.ShouldBe("TestScenario");
        results[0].IterationName.ShouldBe("TestIteration");
    }

    [Fact]
    public async Task DeleteResultsAsync_ShouldCleanupOrphanedExecutions()
    {
        // Arrange
        string executionName = "TestExecution";
        ScenarioRunResult result = CreateTestResult(executionName, "TestScenario", "TestIteration");
        await _store.WriteResultsAsync([result], TestContext.Current.CancellationToken);

        // Act
        await _store.DeleteResultsAsync(executionName, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        using JaimesDbContext context = new(_options);
        bool executionExists = await context.EvaluationExecutions.AnyAsync(e => e.ExecutionName == executionName,
            TestContext.Current.CancellationToken);
        executionExists.ShouldBeFalse();

        bool iterationExists =
            await context.EvaluationScenarioIterations.AnyAsync(si => si.ExecutionName == executionName,
                TestContext.Current.CancellationToken);
        iterationExists.ShouldBeFalse();
    }

    [Fact]
    public async Task GetLatestExecutionNamesAsync_ShouldReturnNamesInOrder()
    {
        // Arrange
        await _store.WriteResultsAsync([CreateTestResult("Execution1", "S", "I")],
            TestContext.Current.CancellationToken);
        await Task.Delay(10, TestContext.Current.CancellationToken); // Ensure timestamp difference
        await _store.WriteResultsAsync([CreateTestResult("Execution2", "S", "I")],
            TestContext.Current.CancellationToken);

        // Act
        List<string> names = new();
        await foreach (string name in _store.GetLatestExecutionNamesAsync(
                           cancellationToken: TestContext.Current.CancellationToken))
        {
            names.Add(name);
        }

        // Assert
        names.Count.ShouldBeGreaterThanOrEqualTo(2);
        names[0].ShouldBe("Execution2");
        names[1].ShouldBe("Execution1");
    }

    private static ScenarioRunResult CreateTestResult(string executionName, string scenarioName, string iterationName)
    {
        return new ScenarioRunResult(
            scenarioName,
            iterationName,
            executionName,
            DateTime.UtcNow,
            new List<ChatMessage>(),
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "Response")),
            new EvaluationResult(),
            null,
            null);
    }
}
