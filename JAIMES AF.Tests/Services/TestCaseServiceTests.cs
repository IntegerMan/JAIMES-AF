using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.Services.Services;
using Microsoft.EntityFrameworkCore;

namespace MattEland.Jaimes.Tests.Services;

public class TestCaseServiceTests : IAsyncLifetime
{
    private JaimesDbContext _context = null!;
    private IDbContextFactory<JaimesDbContext> _contextFactory = null!;
    private TestCaseService _testCaseService = null!;

    public async ValueTask InitializeAsync()
    {
        DbContextOptions<JaimesDbContext> options = new DbContextOptionsBuilder<JaimesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new JaimesDbContext(options);
        await _context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        // Add required test data
        _context.Rulesets.Add(new Ruleset { Id = "test-ruleset", Name = "Test Ruleset" });
        _context.Players.Add(new Player { Id = "test-player", RulesetId = "test-ruleset", Name = "Test Player" });
        _context.Scenarios.Add(new Scenario
        {
            Id = "test-scenario",
            RulesetId = "test-ruleset",
            Name = "Test Scenario"
        });
        _context.Agents.Add(new Agent
        {
            Id = "test-agent",
            Name = "Test Agent",
            Role = "Game Master"
        });
        _context.Models.Add(new Model
        {
            Id = 1,
            Name = "Test Model",
            Provider = "Test",
            Endpoint = "http://test"
        });
        _context.AgentInstructionVersions.Add(new AgentInstructionVersion
        {
            Id = 1,
            AgentId = "test-agent",
            VersionNumber = "1.0",
            Instructions = "Test instructions",
            ModelId = 1
        });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _contextFactory = new TestDbContextFactory(options);
        _testCaseService = new TestCaseService(_contextFactory);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    private async Task<Game> CreateTestGameAsync()
    {
        var game = new Game
        {
            Id = Guid.NewGuid(),
            Title = "Test Game",
            RulesetId = "test-ruleset",
            ScenarioId = "test-scenario",
            PlayerId = "test-player",
            CreatedAt = DateTime.UtcNow
        };
        _context.Games.Add(game);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return game;
    }

    private async Task<Message> CreatePlayerMessageAsync(Guid gameId, string text = "Test player message")
    {
        var message = new Message
        {
            GameId = gameId,
            Text = text,
            PlayerId = "test-player",
            AgentId = "test-agent",
            InstructionVersionId = 1,
            CreatedAt = DateTime.UtcNow
        };
        _context.Messages.Add(message);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return message;
    }

    [Fact]
    public async Task CreateTestCaseAsync_CreatesTestCase_ForPlayerMessage()
    {
        // Arrange
        var game = await CreateTestGameAsync();
        var message = await CreatePlayerMessageAsync(game.Id, "What can I do in this town?");

        // Act
        var result = await _testCaseService.CreateTestCaseAsync(
            message.Id, "Town Actions Test", "Tests player asking about available actions",
            TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBeGreaterThan(0);
        result.Name.ShouldBe("Town Actions Test");
        result.Description.ShouldBe("Tests player asking about available actions");
        result.MessageId.ShouldBe(message.Id);
        result.MessageText.ShouldBe("What can I do in this town?");
        result.IsActive.ShouldBeTrue();
        result.GameId.ShouldBe(game.Id);
        result.AgentId.ShouldBe("test-agent");
    }

    [Fact]
    public async Task CreateTestCaseAsync_ThrowsException_WhenMessageNotFound()
    {
        // Arrange
        int nonExistentMessageId = 100099;

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await _testCaseService.CreateTestCaseAsync(
                nonExistentMessageId, "Test", null,
                TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("100099");
        exception.Message.ShouldContain("not found");
    }

    [Fact]
    public async Task CreateTestCaseAsync_ThrowsException_WhenMessageIsNotPlayerMessage()
    {
        // Arrange
        var game = await CreateTestGameAsync();
        var aiMessage = new Message
        {
            GameId = game.Id,
            Text = "AI response",
            PlayerId = null, // AI message - no player
            AgentId = "test-agent",
            InstructionVersionId = 1,
            CreatedAt = DateTime.UtcNow
        };
        _context.Messages.Add(aiMessage);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await _testCaseService.CreateTestCaseAsync(
                aiMessage.Id, "Test", null,
                TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("player messages");
    }

    [Fact]
    public async Task CreateTestCaseAsync_ThrowsException_WhenTestCaseAlreadyExists()
    {
        // Arrange
        var game = await CreateTestGameAsync();
        var message = await CreatePlayerMessageAsync(game.Id);

        // Create first test case
        await _testCaseService.CreateTestCaseAsync(message.Id, "First", null, TestContext.Current.CancellationToken);

        // Act & Assert - Try to create second test case for same message
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await _testCaseService.CreateTestCaseAsync(
                message.Id, "Second", null,
                TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("already");
    }

    [Fact]
    public async Task GetTestCaseAsync_ReturnsTestCase_WhenExists()
    {
        // Arrange
        var game = await CreateTestGameAsync();
        var message = await CreatePlayerMessageAsync(game.Id);
        var created = await _testCaseService.CreateTestCaseAsync(
            message.Id, "Get Test", "Description", TestContext.Current.CancellationToken);

        // Act
        var result = await _testCaseService.GetTestCaseAsync(created.Id, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(created.Id);
        result.Name.ShouldBe("Get Test");
        result.Description.ShouldBe("Description");
    }

    [Fact]
    public async Task GetTestCaseAsync_ReturnsNull_WhenNotExists()
    {
        // Act
        var result = await _testCaseService.GetTestCaseAsync(100099, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetTestCaseByMessageIdAsync_ReturnsTestCase_WhenExists()
    {
        // Arrange
        var game = await CreateTestGameAsync();
        var message = await CreatePlayerMessageAsync(game.Id);
        var created = await _testCaseService.CreateTestCaseAsync(
            message.Id, "By Message Test", null, TestContext.Current.CancellationToken);

        // Act
        var result =
            await _testCaseService.GetTestCaseByMessageIdAsync(message.Id, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(created.Id);
        result.MessageId.ShouldBe(message.Id);
    }

    [Fact]
    public async Task ListTestCasesAsync_ReturnsActiveTestCases()
    {
        // Arrange
        var game = await CreateTestGameAsync();
        var message1 = await CreatePlayerMessageAsync(game.Id, "Message 1");
        var message2 = await CreatePlayerMessageAsync(game.Id, "Message 2");

        await _testCaseService.CreateTestCaseAsync(message1.Id, "Test 1", null, TestContext.Current.CancellationToken);
        await _testCaseService.CreateTestCaseAsync(message2.Id, "Test 2", null, TestContext.Current.CancellationToken);

        // Act
        var result = await _testCaseService.ListTestCasesAsync(null, false, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task ListTestCasesAsync_FiltersByAgent()
    {
        // Arrange
        var game = await CreateTestGameAsync();
        var message = await CreatePlayerMessageAsync(game.Id);
        await _testCaseService.CreateTestCaseAsync(message.Id, "Agent Filter Test", null,
            TestContext.Current.CancellationToken);

        // Act
        var resultMatching =
            await _testCaseService.ListTestCasesAsync("test-agent", false, TestContext.Current.CancellationToken);
        var resultNonMatching =
            await _testCaseService.ListTestCasesAsync("other-agent", false, TestContext.Current.CancellationToken);

        // Assert
        resultMatching.ShouldNotBeEmpty();
        resultNonMatching.ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteTestCaseAsync_SetsIsActiveToFalse()
    {
        // Arrange
        var game = await CreateTestGameAsync();
        var message = await CreatePlayerMessageAsync(game.Id);
        var created = await _testCaseService.CreateTestCaseAsync(
            message.Id, "Delete Test", null, TestContext.Current.CancellationToken);

        // Act
        var result = await _testCaseService.DeleteTestCaseAsync(created.Id, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeTrue();

        // Verify it's deactivated
        var testCase = await _context.TestCases.FindAsync([created.Id], TestContext.Current.CancellationToken);
        testCase.ShouldNotBeNull();
        testCase.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteTestCaseAsync_ReturnsFalse_WhenNotExists()
    {
        // Act
        var result = await _testCaseService.DeleteTestCaseAsync(100099, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task UpdateTestCaseAsync_UpdatesNameAndDescription()
    {
        // Arrange
        var game = await CreateTestGameAsync();
        var message = await CreatePlayerMessageAsync(game.Id);
        var created = await _testCaseService.CreateTestCaseAsync(
            message.Id, "Original Name", "Original Description", TestContext.Current.CancellationToken);

        // Act
        var result = await _testCaseService.UpdateTestCaseAsync(
            created.Id, "Updated Name", "Updated Description", TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Updated Name");
        result.Description.ShouldBe("Updated Description");
    }

    [Fact]
    public async Task UpdateTestCaseAsync_ReturnsNull_WhenNotExists()
    {
        // Act
        var result = await _testCaseService.UpdateTestCaseAsync(
            100099, "Name", "Desc", TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    private class TestDbContextFactory(DbContextOptions<JaimesDbContext> options) : IDbContextFactory<JaimesDbContext>
    {
        public JaimesDbContext CreateDbContext() => new(options);

        public Task<JaimesDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new JaimesDbContext(options));
    }
}
