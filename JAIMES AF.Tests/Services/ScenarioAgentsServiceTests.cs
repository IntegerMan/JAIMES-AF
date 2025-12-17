using MattEland.Jaimes.ServiceLayer.Services;

namespace MattEland.Jaimes.Tests.Services;

public class ScenarioAgentsServiceTests : IAsyncLifetime
{
    private JaimesDbContext _context = null!;
    private IDbContextFactory<JaimesDbContext> _contextFactory = null!;
    private AgentsService _agentsService = null!;
    private AgentInstructionVersionsService _versionsService = null!;
    private ScenarioAgentsService _scenarioAgentsService = null!;
    private string _agentId = string.Empty;
    private int _versionId = 0;

    public async ValueTask InitializeAsync()
    {
        DbContextOptions<JaimesDbContext> options = new DbContextOptionsBuilder<JaimesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new JaimesDbContext(options);
        await _context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        // Add test data
        _context.Rulesets.Add(new Ruleset { Id = "test-ruleset", Name = "Test Ruleset" });
        _context.Scenarios.Add(new Scenario
        {
            Id = "test-scenario",
            RulesetId = "test-ruleset",
            Name = "Test Scenario"
        });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _contextFactory = new TestDbContextFactory(options);
        _agentsService = new AgentsService(_contextFactory);
        _versionsService = new AgentInstructionVersionsService(_contextFactory);
        _scenarioAgentsService = new ScenarioAgentsService(_contextFactory);

        // Create test agent and version
        AgentDto agent = await _agentsService.CreateAgentAsync("Test Agent", "GameMaster", TestContext.Current.CancellationToken);
        _agentId = agent.Id;
        AgentInstructionVersionDto version = await _versionsService.CreateInstructionVersionAsync(_agentId, "1.0.0", "Test instructions", TestContext.Current.CancellationToken);
        _versionId = version.Id;
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task SetScenarioAgentAsync_CreatesScenarioAgent()
    {
        // Act
        ScenarioAgentDto scenarioAgent = await _scenarioAgentsService.SetScenarioAgentAsync(
            "test-scenario", _agentId, _versionId, TestContext.Current.CancellationToken);

        // Assert
        scenarioAgent.ShouldNotBeNull();
        scenarioAgent.ScenarioId.ShouldBe("test-scenario");
        scenarioAgent.AgentId.ShouldBe(_agentId);
        scenarioAgent.InstructionVersionId.ShouldBe(_versionId);
    }

    [Fact]
    public async Task GetScenarioAgentsAsync_ReturnsScenarioAgents()
    {
        // Arrange
        await _scenarioAgentsService.SetScenarioAgentAsync("test-scenario", _agentId, _versionId, TestContext.Current.CancellationToken);

        // Act
        ScenarioAgentDto[] scenarioAgents = await _scenarioAgentsService.GetScenarioAgentsAsync("test-scenario", TestContext.Current.CancellationToken);

        // Assert
        scenarioAgents.Length.ShouldBe(1);
        scenarioAgents[0].AgentId.ShouldBe(_agentId);
    }

    [Fact]
    public async Task RemoveScenarioAgentAsync_RemovesScenarioAgent()
    {
        // Arrange
        await _scenarioAgentsService.SetScenarioAgentAsync("test-scenario", _agentId, _versionId, TestContext.Current.CancellationToken);

        // Act
        await _scenarioAgentsService.RemoveScenarioAgentAsync("test-scenario", _agentId, TestContext.Current.CancellationToken);

        // Assert
        ScenarioAgentDto[] scenarioAgents = await _scenarioAgentsService.GetScenarioAgentsAsync("test-scenario", TestContext.Current.CancellationToken);
        scenarioAgents.ShouldBeEmpty();
    }

    private class TestDbContextFactory(DbContextOptions<JaimesDbContext> options) : IDbContextFactory<JaimesDbContext>
    {
        public JaimesDbContext CreateDbContext()
        {
            return new JaimesDbContext(options);
        }

        public async Task<JaimesDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            return new JaimesDbContext(options);
        }
    }
}
