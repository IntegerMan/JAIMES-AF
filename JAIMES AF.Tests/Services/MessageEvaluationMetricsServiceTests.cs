using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceLayer.Services;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace MattEland.Jaimes.Tests.Services;

public class MessageEvaluationMetricsServiceTests : IAsyncLifetime
{
    private JaimesDbContext _context = null!;
    private IDbContextFactory<JaimesDbContext> _contextFactory = null!;
    private MessageEvaluationMetricsService _service = null!;

    public async ValueTask InitializeAsync()
    {
        DbContextOptions<JaimesDbContext> options = new DbContextOptionsBuilder<JaimesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new JaimesDbContext(options);
        await _context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        _contextFactory = new TestDbContextFactory(options);
        _service = new MessageEvaluationMetricsService(_contextFactory);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task GetMetricsListAsync_ReturnsExactlyPageSizeItems()
    {
        // Arrange
        int evaluatorCount = 3;
        int messageCount = 10;
        int pageSize = 5;

        await SeedDataAsync(evaluatorCount, messageCount);

        // Act
        var result =
            await _service.GetMetricsListAsync(1, pageSize, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Items.Count().ShouldBe(pageSize);
        result.TotalCount.ShouldBe(evaluatorCount * messageCount);
    }

    [Fact]
    public async Task GetMetricsListAsync_Pagination_ReturnsCorrectItemsForSecondPage()
    {
        // Arrange
        int evaluatorCount = 2;
        int messageCount = 5;
        int pageSize = 3;

        await SeedDataAsync(evaluatorCount, messageCount);

        // Act
        var page1 = await _service.GetMetricsListAsync(1, pageSize,
            cancellationToken: TestContext.Current.CancellationToken);
        var page2 = await _service.GetMetricsListAsync(2, pageSize,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        page1.Items.Count().ShouldBe(pageSize);
        page2.Items.Count().ShouldBe(pageSize);

        // Ensure items are different
        page1.Items.Select(i => i.Id).ShouldNotBe(page2.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task GetMetricsListAsync_IncludesMissingEvaluatorsInTotalCount()
    {
        // Arrange
        int evaluatorCount = 5;
        int messageWithMetricsCount = 2;
        int messageTotalCount = 3;

        // Seed 5 evaluators
        var evaluators = Enumerable.Range(1, evaluatorCount).Select(i => new Evaluator { Name = $"Evaluator {i}" })
            .ToList();
        await _context.Evaluators.AddRangeAsync(evaluators, TestContext.Current.CancellationToken);

        // Seed Agent, Version, and Game dependencies
        var ruleset = new Ruleset { Id = "rules", Name = "Rules" };
        var scenario = new Scenario { Id = "scen", Name = "Scen", RulesetId = ruleset.Id };
        var player = new Player { Id = "player", Name = "Player", RulesetId = ruleset.Id };
        var agent = new Agent { Id = "test-agent", Name = "Test Agent", Role = "GM" };
        var version = new AgentInstructionVersion
            { Id = 1, AgentId = agent.Id, Instructions = "rules", VersionNumber = "v1" };
        var game = new Game
        {
            Id = Guid.NewGuid(), Title = "Test Game", RulesetId = ruleset.Id, ScenarioId = scenario.Id,
            PlayerId = player.Id
        };

        await _context.Rulesets.AddAsync(ruleset, TestContext.Current.CancellationToken);
        await _context.Scenarios.AddAsync(scenario, TestContext.Current.CancellationToken);
        await _context.Players.AddAsync(player, TestContext.Current.CancellationToken);
        await _context.Agents.AddAsync(agent, TestContext.Current.CancellationToken);
        await _context.AgentInstructionVersions.AddAsync(version, TestContext.Current.CancellationToken);
        await _context.Games.AddAsync(game, TestContext.Current.CancellationToken);

        // Seed 3 messages
        var messages = Enumerable.Range(1, messageTotalCount).Select(i => new Message
        {
            Text = $"Message {i}",
            IsScriptedMessage = false,
            AgentId = agent.Id,
            InstructionVersionId = version.Id,
            GameId = game.Id,
            PlayerId = null,
            CreatedAt = DateTime.UtcNow
        }).ToList();
        await _context.Messages.AddRangeAsync(messages, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Add 1 metric per evaluator for only the first 2 messages
        foreach (var msg in messages.Take(messageWithMetricsCount))
        {
            foreach (var eval in evaluators)
            {
                await _context.MessageEvaluationMetrics.AddAsync(new MessageEvaluationMetric
                {
                    MessageId = msg.Id,
                    EvaluatorId = eval.Id,
                    MetricName = eval.Name,
                    Score = 4.0,
                    EvaluatedAt = DateTime.UtcNow
                }, TestContext.Current.CancellationToken);
            }
        }

        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result =
            await _service.GetMetricsListAsync(1, 100, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        // Total count should be MessageTotalCount * EvaluatorCount
        // 3 messages * 5 evaluators = 15 total items (some missing, some not)
        result.TotalCount.ShouldBe(15);
        result.Items.Count().ShouldBe(15);
        result.Items.Count(i => i.IsMissing).ShouldBe(5); // The 3rd message has 5 missing evaluators
    }

    private async Task SeedDataAsync(int evaluatorCount, int messageCount)
    {
        var evaluators = Enumerable.Range(1, evaluatorCount).Select(i => new Evaluator { Name = $"Evaluator {i}" })
            .ToList();
        await _context.Evaluators.AddRangeAsync(evaluators, TestContext.Current.CancellationToken);

        // Seed Agent, Version, and Game dependencies
        var ruleset = new Ruleset { Id = "rules", Name = "Rules" };
        var scenario = new Scenario { Id = "scen", Name = "Scen", RulesetId = ruleset.Id };
        var player = new Player { Id = "player", Name = "Player", RulesetId = ruleset.Id };
        var agent = new Agent { Id = "test-agent", Name = "Test Agent", Role = "GM" };
        var version = new AgentInstructionVersion
            { Id = 1, AgentId = agent.Id, Instructions = "rules", VersionNumber = "v1" };
        var game = new Game
        {
            Id = Guid.NewGuid(), Title = "Test Game", RulesetId = ruleset.Id, ScenarioId = scenario.Id,
            PlayerId = player.Id
        };

        await _context.Rulesets.AddAsync(ruleset, TestContext.Current.CancellationToken);
        await _context.Scenarios.AddAsync(scenario, TestContext.Current.CancellationToken);
        await _context.Players.AddAsync(player, TestContext.Current.CancellationToken);
        await _context.Agents.AddAsync(agent, TestContext.Current.CancellationToken);
        await _context.AgentInstructionVersions.AddAsync(version, TestContext.Current.CancellationToken);
        await _context.Games.AddAsync(game, TestContext.Current.CancellationToken);

        var messages = Enumerable.Range(1, messageCount).Select(i => new Message
        {
            Text = $"Message {i}",
            IsScriptedMessage = false,
            AgentId = agent.Id,
            InstructionVersionId = version.Id,
            GameId = game.Id,
            PlayerId = null,
            CreatedAt = DateTime.UtcNow
        }).ToList();
        await _context.Messages.AddRangeAsync(messages, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        foreach (var msg in messages)
        {
            foreach (var eval in evaluators)
            {
                await _context.MessageEvaluationMetrics.AddAsync(new MessageEvaluationMetric
                {
                    MessageId = msg.Id,
                    EvaluatorId = eval.Id,
                    MetricName = eval.Name,
                    Score = 4.0,
                    EvaluatedAt = DateTime.UtcNow
                }, TestContext.Current.CancellationToken);
            }
        }

        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private class TestDbContextFactory(DbContextOptions<JaimesDbContext> options) : IDbContextFactory<JaimesDbContext>
    {
        public JaimesDbContext CreateDbContext() => new JaimesDbContext(options);

        public Task<JaimesDbContext> CreateDbContextAsync(CancellationToken ct = default) =>
            Task.FromResult(new JaimesDbContext(options));
    }
}
