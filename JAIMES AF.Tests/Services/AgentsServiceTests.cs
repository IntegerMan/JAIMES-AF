using MattEland.Jaimes.ServiceLayer.Services;

namespace MattEland.Jaimes.Tests.Services;

public class AgentsServiceTests : IAsyncLifetime
{
    private JaimesDbContext _context = null!;
    private IDbContextFactory<JaimesDbContext> _contextFactory = null!;
    private AgentsService _agentsService = null!;

    public async ValueTask InitializeAsync()
    {
        DbContextOptions<JaimesDbContext> options = new DbContextOptionsBuilder<JaimesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new JaimesDbContext(options);
        await _context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        _contextFactory = new TestDbContextFactory(options);
        _agentsService = new AgentsService(_contextFactory);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task GetAgentsAsync_ReturnsEmptyArray_WhenNoAgentsExist()
    {
        // Act
        AgentDto[] agents = await _agentsService.GetAgentsAsync(TestContext.Current.CancellationToken);

        // Assert
        agents.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateAgentAsync_CreatesAgent()
    {
        // Act
        AgentDto agent = await _agentsService.CreateAgentAsync("Test Agent", "GameMaster", "Test instructions", TestContext.Current.CancellationToken);

        // Assert
        agent.ShouldNotBeNull();
        agent.Name.ShouldBe("Test Agent");
        agent.Role.ShouldBe("GameMaster");
        agent.Id.ShouldBe("testagent");
    }

    [Fact]
    public async Task GetAgentAsync_ReturnsAgent_WhenExists()
    {
        // Arrange
        AgentDto created = await _agentsService.CreateAgentAsync("Test Agent", "GameMaster", "Test instructions", TestContext.Current.CancellationToken);

        // Act
        AgentDto? retrieved = await _agentsService.GetAgentAsync(created.Id, TestContext.Current.CancellationToken);

        // Assert
        retrieved.ShouldNotBeNull();
        retrieved!.Id.ShouldBe(created.Id);
        retrieved.Name.ShouldBe("Test Agent");
    }

    [Fact]
    public async Task UpdateAgentAsync_UpdatesAgent()
    {
        // Arrange
        AgentDto created = await _agentsService.CreateAgentAsync("Test Agent", "GameMaster", "Test instructions", TestContext.Current.CancellationToken);

        // Act
        AgentDto updated = await _agentsService.UpdateAgentAsync(created.Id, "Updated Agent", "GameMaster", TestContext.Current.CancellationToken);

        // Assert
        updated.Name.ShouldBe("Updated Agent");
        updated.Role.ShouldBe("GameMaster");
    }

    [Fact]
    public async Task DeleteAgentAsync_DeletesAgent()
    {
        // Arrange
        AgentDto created = await _agentsService.CreateAgentAsync("Test Agent", "GameMaster", "Test instructions", TestContext.Current.CancellationToken);

        // Act
        await _agentsService.DeleteAgentAsync(created.Id, TestContext.Current.CancellationToken);

        // Assert
        AgentDto? retrieved = await _agentsService.GetAgentAsync(created.Id, TestContext.Current.CancellationToken);
        retrieved.ShouldBeNull();
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
