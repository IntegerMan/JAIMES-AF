using MattEland.Jaimes.ApiService.Services;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;

namespace MattEland.Jaimes.Tests.Services;

public class AgentTestRunnerTests : IAsyncLifetime
{
    private JaimesDbContext _context = null!;
    private IDbContextFactory<JaimesDbContext> _contextFactory = null!;
    private Mock<IChatClient> _mockChatClient = null!;
    private Mock<ILogger<AgentTestRunner>> _mockLogger = null!;
    private AgentTestRunner _testRunner = null!;
    private TestCase _testCase = null!;

    public async ValueTask InitializeAsync()
    {
        DbContextOptions<JaimesDbContext> options = new DbContextOptionsBuilder<JaimesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new JaimesDbContext(options);
        await _context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        // Seed test data
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
            ModelId = 1,
            IsActive = true
        });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Create game and message for test case
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

        var message = new Message
        {
            GameId = game.Id,
            Text = "What can I do here?",
            PlayerId = "test-player",
            AgentId = "test-agent",
            InstructionVersionId = 1,
            CreatedAt = DateTime.UtcNow
        };
        _context.Messages.Add(message);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Create test case
        _testCase = new TestCase
        {
            MessageId = message.Id,
            Name = "Test Case",
            Description = "Test description",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.TestCases.Add(_testCase);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _contextFactory = new TestDbContextFactory(options);
        _mockChatClient = new Mock<IChatClient>();
        _mockLogger = new Mock<ILogger<AgentTestRunner>>();

        // Setup mock chat client to return a simple response
        SetupMockChatClient("This is the agent's test response.");

        _testRunner = new AgentTestRunner(_contextFactory, _mockChatClient.Object, _mockLogger.Object);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    private void SetupMockChatClient(string responseText)
    {
        var mockResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText));

        _mockChatClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);
    }

    [Fact]
    public async Task RunTestCasesAsync_ThrowsException_WhenAgentNotFound()
    {
        // Arrange
        string nonExistentAgentId = "nonexistent-agent";

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await _testRunner.RunTestCasesAsync(nonExistentAgentId, 1, null, null, TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("nonexistent-agent");
        exception.Message.ShouldContain("not found");
    }

    [Fact]
    public async Task RunTestCasesAsync_ThrowsException_WhenVersionNotFound()
    {
        // Arrange
        int nonExistentVersionId = 99999;

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await _testRunner.RunTestCasesAsync("test-agent", nonExistentVersionId, null, null, TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("99999");
        exception.Message.ShouldContain("not found");
    }

    [Fact]
    public async Task RunTestCasesAsync_ThrowsException_WhenVersionBelongsToDifferentAgent()
    {
        // Arrange - Create another agent
        _context.Agents.Add(new Agent { Id = "other-agent", Name = "Other Agent", Role = "NPC" });
        _context.AgentInstructionVersions.Add(new AgentInstructionVersion
        {
            Id = 2,
            AgentId = "other-agent",
            VersionNumber = "1.0",
            Instructions = "Other instructions",
            ModelId = 1
        });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert - Try to use version 2 (belongs to other-agent) with test-agent
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await _testRunner.RunTestCasesAsync("test-agent", 2, null, null, TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("not found");
    }

    [Fact]
    public async Task RunTestCasesAsync_ThrowsException_WhenNoTestCasesFound()
    {
        // Arrange - Deactivate all test cases
        _testCase.IsActive = false;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await _testRunner.RunTestCasesAsync("test-agent", 1, null, null, TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("No test cases found");
    }

    [Fact]
    public async Task RunTestCasesAsync_ReturnsResult_WithCorrectMetadata()
    {
        // Act
        var result = await _testRunner.RunTestCasesAsync(
            "test-agent", 1, null, "test-execution", TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.AgentId.ShouldBe("test-agent");
        result.AgentName.ShouldBe("Test Agent");
        result.InstructionVersionId.ShouldBe(1);
        result.VersionNumber.ShouldBe("1.0");
        result.ExecutionName.ShouldBe("test-execution");
        result.TotalTestCases.ShouldBe(1);
        result.CompletedTestCases.ShouldBe(1);
        result.FailedTestCases.ShouldBe(0);
    }

    [Fact]
    public async Task RunTestCasesAsync_GeneratesExecutionName_WhenNotProvided()
    {
        // Act
        var result = await _testRunner.RunTestCasesAsync(
            "test-agent", 1, null, null, TestContext.Current.CancellationToken);

        // Assert
        result.ExecutionName.ShouldNotBeNullOrEmpty();
        result.ExecutionName.ShouldStartWith("test-run-test-agent-1-");
    }

    [Fact]
    public async Task RunTestCasesAsync_FiltersTestCases_ByProvidedIds()
    {
        // Arrange - Create another test case
        var game = await _context.Games.FirstAsync(TestContext.Current.CancellationToken);
        var message2 = new Message
        {
            GameId = game.Id,
            Text = "Second test message",
            PlayerId = "test-player",
            AgentId = "test-agent",
            InstructionVersionId = 1,
            CreatedAt = DateTime.UtcNow
        };
        _context.Messages.Add(message2);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var testCase2 = new TestCase
        {
            MessageId = message2.Id,
            Name = "Test Case 2",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.TestCases.Add(testCase2);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act - Only run the first test case
        var result = await _testRunner.RunTestCasesAsync(
            "test-agent", 1, [_testCase.Id], null, TestContext.Current.CancellationToken);

        // Assert
        result.TotalTestCases.ShouldBe(1);
        result.Runs.ShouldHaveSingleItem();
        result.Runs[0].TestCaseId.ShouldBe(_testCase.Id);
    }

    [Fact]
    public async Task RunTestCasesAsync_PersistsTestCaseRun_ToDatabase()
    {
        // Act
        var result = await _testRunner.RunTestCasesAsync(
            "test-agent", 1, null, "persist-test", TestContext.Current.CancellationToken);

        // Assert - Verify run was persisted
        var persistedRun = await _context.TestCaseRuns
            .FirstOrDefaultAsync(r => r.ExecutionName == "persist-test", TestContext.Current.CancellationToken);

        persistedRun.ShouldNotBeNull();
        persistedRun.TestCaseId.ShouldBe(_testCase.Id);
        persistedRun.AgentId.ShouldBe("test-agent");
        persistedRun.InstructionVersionId.ShouldBe(1);
        persistedRun.GeneratedResponse.ShouldNotBeNullOrEmpty();
        persistedRun.DurationMs.ShouldNotBeNull();
    }

    private class TestDbContextFactory(DbContextOptions<JaimesDbContext> options) : IDbContextFactory<JaimesDbContext>
    {
        public JaimesDbContext CreateDbContext() => new(options);
        public Task<JaimesDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new JaimesDbContext(options));
    }
}
