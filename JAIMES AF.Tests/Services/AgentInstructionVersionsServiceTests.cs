using MattEland.Jaimes.ServiceLayer.Services;

namespace MattEland.Jaimes.Tests.Services;

public class AgentInstructionVersionsServiceTests : IAsyncLifetime
{
    private JaimesDbContext _context = null!;
    private IDbContextFactory<JaimesDbContext> _contextFactory = null!;
    private AgentsService _agentsService = null!;
    private AgentInstructionVersionsService _versionsService = null!;
    private string _agentId = string.Empty;

    public async ValueTask InitializeAsync()
    {
        DbContextOptions<JaimesDbContext> options = new DbContextOptionsBuilder<JaimesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new JaimesDbContext(options);
        await _context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        _contextFactory = new TestDbContextFactory(options);
        _agentsService = new AgentsService(_contextFactory);
        _versionsService = new AgentInstructionVersionsService(_contextFactory);

        // Create a test agent
        AgentDto agent = await _agentsService.CreateAgentAsync("Test Agent", "GameMaster", "Test instructions", TestContext.Current.CancellationToken);
        _agentId = agent.Id;
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task CreateInstructionVersionAsync_CreatesVersion()
    {
        // Act
        AgentInstructionVersionDto version = await _versionsService.CreateInstructionVersionAsync(
            _agentId, "1.0.0", "Test instructions", TestContext.Current.CancellationToken);

        // Assert
        version.ShouldNotBeNull();
        version.AgentId.ShouldBe(_agentId);
        version.VersionNumber.ShouldBe("1.0.0");
        version.Instructions.ShouldBe("Test instructions");
        version.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task GetInstructionVersionsAsync_ReturnsVersions()
    {
        // Arrange
        // Note: Agent creation automatically creates a v1.0 version, so we expect 3 total versions
        await _versionsService.CreateInstructionVersionAsync(_agentId, "1.0.0", "Instructions 1", TestContext.Current.CancellationToken);
        await _versionsService.CreateInstructionVersionAsync(_agentId, "2.0.0", "Instructions 2", TestContext.Current.CancellationToken);

        // Act
        AgentInstructionVersionDto[] versions = await _versionsService.GetInstructionVersionsAsync(_agentId, TestContext.Current.CancellationToken);

        // Assert
        // v1.0 (from agent creation) + 1.0.0 + 2.0.0 = 3 versions
        versions.Length.ShouldBe(3);
    }

    [Fact]
    public async Task GetActiveInstructionVersionAsync_ReturnsActiveVersion()
    {
        // Arrange
        await _versionsService.CreateInstructionVersionAsync(_agentId, "1.0.0", "Instructions 1", TestContext.Current.CancellationToken);
        await _versionsService.CreateInstructionVersionAsync(_agentId, "2.0.0", "Instructions 2", TestContext.Current.CancellationToken);

        // Act
        AgentInstructionVersionDto? active = await _versionsService.GetActiveInstructionVersionAsync(_agentId, TestContext.Current.CancellationToken);

        // Assert
        active.ShouldNotBeNull();
        active!.VersionNumber.ShouldBe("2.0.0"); // The latest created version should be active
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
