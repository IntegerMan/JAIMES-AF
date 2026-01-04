using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefaults;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.Workers.AssistantMessageWorker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Reporting;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace MattEland.Jaimes.Tests.Services;

public class MessageEvaluationServiceTests : IAsyncLifetime
{
    private JaimesDbContext _context = null!;
    private IDbContextFactory<JaimesDbContext> _contextFactory = null!;
    private MessageEvaluationService _service = null!;
    private IChatClient _chatClient = null!;
    private IEvaluationResultStore _resultStore = null!;
    private IMessageUpdateNotifier _messageUpdateNotifier = null!;
    private ILogger<MessageEvaluationService> _logger = null!;
    private TextGenerationModelOptions _modelOptions = null!;

    public async ValueTask InitializeAsync()
    {
        // Create an in-memory database for testing
        DbContextOptions<JaimesDbContext> options = new DbContextOptionsBuilder<JaimesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new JaimesDbContext(options);
        await _context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        // Seed required data
        var ruleset = new Ruleset { Id = "test-ruleset", Name = "Test Ruleset" };
        var scenario = new Scenario { Id = "test-scenario", RulesetId = ruleset.Id, Name = "Test Scenario" };
        var player = new Player { Id = "test-player", RulesetId = ruleset.Id, Name = "Test Player" };
        var agent = new Agent { Id = "test-agent", Name = "Test Agent", Role = "GM" };
        var model = new Model { Id = 1, Name = "test-model", Provider = "test", Endpoint = "http://test" };
        var version = new AgentInstructionVersion
        {
            Id = 1,
            AgentId = agent.Id,
            VersionNumber = "v1",
            Instructions = "Test instructions",
            ModelId = model.Id
        };
        var game = new Game
        {
            Id = Guid.NewGuid(),
            Title = "Test Game",
            RulesetId = ruleset.Id,
            ScenarioId = scenario.Id,
            PlayerId = player.Id
        };

        _context.Rulesets.Add(ruleset);
        _context.Scenarios.Add(scenario);
        _context.Players.Add(player);
        _context.Agents.Add(agent);
        _context.Models.Add(model);
        _context.AgentInstructionVersions.Add(version);
        _context.Games.Add(game);

        // Add evaluators to database
        _context.Evaluators.Add(new Evaluator { Id = 1, Name = "TestEvaluator1" });
        _context.Evaluators.Add(new Evaluator { Id = 2, Name = "TestEvaluator2" });

        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _contextFactory = new TestDbContextFactory(options);

        // Create mocks
        _chatClient = new Mock<IChatClient>().Object;
        _resultStore = new Mock<IEvaluationResultStore>().Object;
        _messageUpdateNotifier = new Mock<IMessageUpdateNotifier>().Object;
        _logger = new Mock<ILogger<MessageEvaluationService>>().Object;
        _modelOptions = new TextGenerationModelOptions
        {
            Name = "test-model"
        };
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task EvaluateMessageAsync_CallsEachEvaluatorExactlyOnce()
    {
        // Arrange
        var game = await _context.Games.FirstAsync(TestContext.Current.CancellationToken);
        var message = new Message
        {
            Id = 1,
            GameId = game.Id,
            Text = "This is a test assistant message",
            PlayerId = null, // Assistant message
            AgentId = "test-agent",
            InstructionVersionId = 1,
            CreatedAt = DateTime.UtcNow
        };
        await _context.Messages.AddAsync(message, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Create mock evaluators that track call counts
        int evaluator1CallCount = 0;
        int evaluator2CallCount = 0;
        int evaluator3CallCount = 0;

        var mockEvaluator1 = new TestEvaluator1(() => evaluator1CallCount++);
        var mockEvaluator2 = new TestEvaluator2(() => evaluator2CallCount++);
        var mockEvaluator3 = new TestEvaluator3(() => evaluator3CallCount++);

        var evaluators = new IEvaluator[] { mockEvaluator1, mockEvaluator2, mockEvaluator3 };

        _service = new MessageEvaluationService(
            _contextFactory,
            evaluators,
            _chatClient,
            _modelOptions,
            _resultStore,
            _logger,
            _messageUpdateNotifier);

        // Act
        await _service.EvaluateMessageAsync(
            message,
            "Test system prompt",
            [],
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        evaluator1CallCount.ShouldBe(1, "TestEvaluator1 should be called exactly once");
        evaluator2CallCount.ShouldBe(1, "TestEvaluator2 should be called exactly once");
        evaluator3CallCount.ShouldBe(1, "TestEvaluator3 should be called exactly once");
    }

    [Fact]
    public async Task EvaluateMessageAsync_WithSpecificEvaluators_CallsOnlySpecifiedEvaluatorsOnce()
    {
        // Arrange
        var game = await _context.Games.FirstAsync(TestContext.Current.CancellationToken);
        var message = new Message
        {
            Id = 2,
            GameId = game.Id,
            Text = "Another test message",
            PlayerId = null,
            AgentId = "test-agent",
            InstructionVersionId = 1,
            CreatedAt = DateTime.UtcNow
        };
        await _context.Messages.AddAsync(message, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        int evaluator1CallCount = 0;
        int evaluator2CallCount = 0;
        int evaluator3CallCount = 0;

        var mockEvaluator1 = new TestEvaluator1(() => evaluator1CallCount++);
        var mockEvaluator2 = new TestEvaluator2(() => evaluator2CallCount++);
        var mockEvaluator3 = new TestEvaluator3(() => evaluator3CallCount++);

        var evaluators = new IEvaluator[] { mockEvaluator1, mockEvaluator2, mockEvaluator3 };

        _service = new MessageEvaluationService(
            _contextFactory,
            evaluators,
            _chatClient,
            _modelOptions,
            _resultStore,
            _logger,
            _messageUpdateNotifier);

        // Act - only run evaluator1 and evaluator3
        await _service.EvaluateMessageAsync(
            message,
            "Test system prompt",
            [],
            evaluatorsToRun: new[] { "TestEvaluator1", "TestEvaluator3" },
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        evaluator1CallCount.ShouldBe(1, "TestEvaluator1 should be called exactly once");
        evaluator2CallCount.ShouldBe(0, "TestEvaluator2 should NOT be called");
        evaluator3CallCount.ShouldBe(1, "TestEvaluator3 should be called exactly once");
    }

    /// <summary>
    /// Mock evaluator that counts how many times it's called
    /// </summary>
    private class CallCountingEvaluator : IEvaluator
    {
        private readonly string _metricName;
        private readonly Action _onCalled;

        public CallCountingEvaluator(string metricName, Action onCalled)
        {
            _metricName = metricName;
            _onCalled = onCalled;
        }

        public IReadOnlyCollection<string> EvaluationMetricNames => [_metricName];

        public ValueTask<EvaluationResult> EvaluateAsync(
            IEnumerable<ChatMessage> messages,
            ChatResponse modelResponse,
            ChatConfiguration? chatConfiguration = null,
            IEnumerable<EvaluationContext>? evaluationContext = null,
            CancellationToken cancellationToken = default)
        {
            // Track that this evaluator was called
            _onCalled();

            // Return a simple evaluation result
            return ValueTask.FromResult(new EvaluationResult
            {
                Metrics = new Dictionary<string, EvaluationMetric>
                {
                    [_metricName] = new NumericMetric(_metricName)
                    {
                        Value = 1.0,
                        Reason = $"{_metricName} evaluation complete"
                    }
                }
            });
        }
    }

    // Named evaluator types so GetType() returns the correct name
    private class TestEvaluator1 : CallCountingEvaluator
    {
        public TestEvaluator1(Action onCalled) : base(nameof(TestEvaluator1), onCalled) { }
    }

    private class TestEvaluator2 : CallCountingEvaluator
    {
        public TestEvaluator2(Action onCalled) : base(nameof(TestEvaluator2), onCalled) { }
    }

    private class TestEvaluator3 : CallCountingEvaluator
    {
        public TestEvaluator3(Action onCalled) : base(nameof(TestEvaluator3), onCalled) { }
    }

    private class TestDbContextFactory(DbContextOptions<JaimesDbContext> options) : IDbContextFactory<JaimesDbContext>
    {
        public JaimesDbContext CreateDbContext() => new(options);
    }
}
