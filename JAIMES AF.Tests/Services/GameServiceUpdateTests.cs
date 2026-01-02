using MattEland.Jaimes.ServiceLayer.Services;
using MattEland.Jaimes.Repositories;
using Xunit;

namespace MattEland.Jaimes.Tests.Services;

public class GameServiceUpdateTests : IAsyncLifetime
{
    private JaimesDbContext _context = null!;
    private IDbContextFactory<JaimesDbContext> _contextFactory = null!;
    private IMessagePublisher _messagePublisher = null!;
    private GameService _gameService = null!;

    public async ValueTask InitializeAsync()
    {
        DbContextOptions<JaimesDbContext> options = new DbContextOptionsBuilder<JaimesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new JaimesDbContext(options);
        await _context.Database.EnsureCreatedAsync();

        // Setup common data
        _context.Rulesets.Add(new Ruleset { Id = "test-ruleset", Name = "Test Ruleset" });
        _context.Players.Add(new Player { Id = "test-player", RulesetId = "test-ruleset", Name = "Test Player" });
        _context.Scenarios.Add(
            new Scenario { Id = "test-scenario", RulesetId = "test-ruleset", Name = "Test Scenario" });

        // Agent 1
        _context.Agents.Add(new Agent { Id = "agent-1", Name = "Agent 1", Role = "Game Master" });
        _context.AgentInstructionVersions.Add(new AgentInstructionVersion
            { Id = 101, AgentId = "agent-1", VersionNumber = "1.0", Instructions = "..." });

        // Agent 2
        _context.Agents.Add(new Agent { Id = "agent-2", Name = "Agent 2", Role = "Game Master" });
        _context.AgentInstructionVersions.Add(new AgentInstructionVersion
            { Id = 201, AgentId = "agent-2", VersionNumber = "1.0", Instructions = "..." });

        // Scenario Agent Mapping (Agent 1 is default for test-scenario)
        _context.ScenarioAgents.Add(new ScenarioAgent
            { ScenarioId = "test-scenario", AgentId = "agent-1", InstructionVersionId = 101 });

        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _contextFactory = new TestDbContextFactory(options);
        _messagePublisher = new MockMessagePublisher();
        _gameService = new GameService(_contextFactory, _messagePublisher);
    }

    public async ValueTask DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task UpdateGameAsync_AllowSettingCorrectVersion_ForOverriddenAgent()
    {
        // Arrange
        Guid gameId = Guid.NewGuid();
        _context.Games.Add(new Game
        {
            Id = gameId,
            Title = "Test Game",
            RulesetId = "test-ruleset",
            ScenarioId = "test-scenario",
            PlayerId = "test-player",
            AgentId = "agent-1",
            InstructionVersionId = 101
        });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act - Switch to Agent 2 and Version 201
        var result =
            await _gameService.UpdateGameAsync(gameId, null, "agent-2", 201, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.AgentId.ShouldBe("agent-2");
        result.InstructionVersionId.ShouldBe(201);
    }

    [Fact]
    public async Task UpdateGameAsync_Throws_WhenVersionDoesNotBelongToProvidedAgent()
    {
        // Arrange
        Guid gameId = Guid.NewGuid();
        _context.Games.Add(new Game
        {
            Id = gameId,
            Title = "Test Game",
            RulesetId = "test-ruleset",
            ScenarioId = "test-scenario",
            PlayerId = "test-player",
            AgentId = "agent-1",
            InstructionVersionId = 101
        });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert - Try to set Agent 2 but Version 101 (which belongs to Agent 1)
        await Should.ThrowAsync<ArgumentException>(async () =>
            await _gameService.UpdateGameAsync(gameId, null, "agent-2", 101, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task UpdateGameAsync_Throws_WhenVersionDoesNotBelongToCurrentGameAgent()
    {
        // Arrange
        Guid gameId = Guid.NewGuid();
        _context.Games.Add(new Game
        {
            Id = gameId,
            Title = "Test Game",
            RulesetId = "test-ruleset",
            ScenarioId = "test-scenario",
            PlayerId = "test-player",
            AgentId = "agent-1",
            InstructionVersionId = 101
        });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert - Try to update version to 201 (belongs to Agent 2) without changing agent (currently Agent 1)
        await Should.ThrowAsync<ArgumentException>(async () =>
            await _gameService.UpdateGameAsync(gameId, null, null, 201, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task UpdateGameAsync_Throws_WhenVersionDoesNotBelongToScenarioAgent_WhenNoOverrideIsPresent()
    {
        // Arrange
        Guid gameId = Guid.NewGuid();
        _context.Games.Add(new Game
        {
            Id = gameId,
            Title = "Test Game",
            RulesetId = "test-ruleset",
            ScenarioId = "test-scenario",
            PlayerId = "test-player",
            AgentId = null, // No override, so agent-1 from scenario is effective
            InstructionVersionId = null
        });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert - Try to set version 201 (Agent 2) when effective agent is Agent 1
        await Should.ThrowAsync<ArgumentException>(async () =>
            await _gameService.UpdateGameAsync(gameId, null, null, 201, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task UpdateGameAsync_Succeeds_WhenVersionBelongsToScenarioAgent_WhenNoOverrideIsPresent()
    {
        // Arrange
        Guid gameId = Guid.NewGuid();
        _context.Games.Add(new Game
        {
            Id = gameId,
            Title = "Test Game",
            RulesetId = "test-ruleset",
            ScenarioId = "test-scenario",
            PlayerId = "test-player",
            AgentId = null,
            InstructionVersionId = null
        });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act - Set version 101 (Agent 1, which is the scenario's agent)
        var result = await _gameService.UpdateGameAsync(gameId, null, null, 101, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.InstructionVersionId.ShouldBe(101);
    }

    private class TestDbContextFactory(DbContextOptions<JaimesDbContext> options) : IDbContextFactory<JaimesDbContext>
    {
        public JaimesDbContext CreateDbContext() => new JaimesDbContext(options);

        public Task<JaimesDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new JaimesDbContext(options));
    }

    private class MockMessagePublisher : IMessagePublisher
    {
        public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class =>
            Task.CompletedTask;
    }
}
