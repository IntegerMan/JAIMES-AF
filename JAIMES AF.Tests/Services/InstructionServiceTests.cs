using MattEland.Jaimes.ServiceLayer.Services;

namespace MattEland.Jaimes.Tests.Services;

public class InstructionServiceTests : IAsyncLifetime
{
    private JaimesDbContext _context = null!;
    private IDbContextFactory<JaimesDbContext> _contextFactory = null!;
    private AgentsService _agentsService = null!;
    private AgentInstructionVersionsService _versionsService = null!;
    private ScenarioAgentsService _scenarioAgentsService = null!;
    private InstructionService _instructionService = null!;
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
        _instructionService = new InstructionService(_contextFactory);

        // Create test agent and version
        AgentDto agent = await _agentsService.CreateAgentAsync("Test Agent", "GameMaster", TestContext.Current.CancellationToken);
        _agentId = agent.Id;
        AgentInstructionVersionDto version = await _versionsService.CreateInstructionVersionAsync(_agentId, "1.0.0", "Base instructions", TestContext.Current.CancellationToken);
        _versionId = version.Id;
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task GetInstructionsAsync_ReturnsBaseInstructions_WhenNoScenarioInstructions()
    {
        // Arrange
        await _scenarioAgentsService.SetScenarioAgentAsync("test-scenario", _agentId, _versionId, TestContext.Current.CancellationToken);

        // Act
        string? instructions = await _instructionService.GetInstructionsAsync("test-scenario", TestContext.Current.CancellationToken);

        // Assert
        instructions.ShouldNotBeNull();
        instructions.ShouldBe("Base instructions");
    }

    [Fact]
    public async Task GetInstructionsAsync_ReturnsCombinedInstructions_WhenScenarioInstructionsExist()
    {
        // Arrange
        await _scenarioAgentsService.SetScenarioAgentAsync("test-scenario", _agentId, _versionId, TestContext.Current.CancellationToken);
        Scenario? scenario = await _context.Scenarios.FindAsync(["test-scenario"], TestContext.Current.CancellationToken);
        scenario!.ScenarioInstructions = "Scenario-specific context";
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        string? instructions = await _instructionService.GetInstructionsAsync("test-scenario", TestContext.Current.CancellationToken);

        // Assert
        instructions.ShouldNotBeNull();
        instructions.ShouldContain("Base instructions");
        instructions.ShouldContain("Scenario-specific context");
        instructions.ShouldContain("---");
    }

    [Fact]
    public async Task GetInstructionsAsync_ReturnsNull_WhenNoAgentConfigured()
    {
        // Act
        string? instructions = await _instructionService.GetInstructionsAsync("test-scenario", TestContext.Current.CancellationToken);

        // Assert
        instructions.ShouldBeNull();
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
