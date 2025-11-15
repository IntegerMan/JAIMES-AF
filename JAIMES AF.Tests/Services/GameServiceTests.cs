using MattEland.Jaimes.Domain;
using Microsoft.EntityFrameworkCore;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.ServiceLayer.Services;
using Shouldly;

namespace MattEland.Jaimes.Tests.Services;

public class GameServiceTests : IAsyncLifetime
{
    private JaimesDbContext _context = null!;
    private GameService _gameService = null!;
    private MockChatService _mockChatService = null!;
    private MockChatHistoryService _mockChatHistoryService = null!;

    public async ValueTask InitializeAsync()
    {
        // Create an in-memory database for testing
        DbContextOptions<JaimesDbContext> options = new DbContextOptionsBuilder<JaimesDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new JaimesDbContext(options);
        await _context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        // Add test data for validation
        _context.Rulesets.Add(new Ruleset { Id = "test-ruleset", Name = "Test Ruleset" });
        _context.Players.Add(new Player { Id = "test-player", RulesetId = "test-ruleset", Name = "Unspecified" });
        _context.Scenarios.Add(new Scenario { Id = "test-scenario", RulesetId = "test-ruleset", Name = "Unspecified", SystemPrompt = "Test System Prompt", NewGameInstructions = "Test New Game Instructions" });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _mockChatService = new MockChatService();
        _mockChatHistoryService = new MockChatHistoryService();
        _gameService = new GameService(_context, _mockChatService, _mockChatHistoryService);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task CreateGameAsync_CreatesGameWithInitialMessage()
    {
        // Arrange
        string scenarioId = "test-scenario";
        string playerId = "test-player";

        // Act
        GameDto gameDto = await _gameService.CreateGameAsync(scenarioId, playerId, TestContext.Current.CancellationToken);

        // Assert
        gameDto.GameId.ShouldNotBe(Guid.Empty);
        gameDto.Ruleset.Id.ShouldBe("test-ruleset");
        gameDto.Scenario.Id.ShouldBe(scenarioId);
        gameDto.Player.Id.ShouldBe(playerId);
        gameDto.Messages.ShouldHaveSingleItem();
        gameDto.Messages[0].Text.ShouldBe(MockChatService.TestInitialMessage);
        gameDto.Messages[0].ParticipantName.ShouldBe("Game Master");

        // Verify game is in database
        Game? gameInDb = await _context.Games.FindAsync(new object[] { gameDto.GameId }, TestContext.Current.CancellationToken);
        gameInDb.ShouldNotBeNull();
        gameInDb.RulesetId.ShouldBe("test-ruleset");

        // Verify message is in database
        Message? messageInDb = await _context.Messages.FirstOrDefaultAsync(m => m.GameId == gameDto.GameId, TestContext.Current.CancellationToken);
        messageInDb.ShouldNotBeNull();
        messageInDb.Text.ShouldBe(MockChatService.TestInitialMessage);
        messageInDb.PlayerId.ShouldBeNull(); // AI-generated message
    }

    [Fact]
    public async Task GetGameAsync_ReturnsNull_WhenGameDoesNotExist()
    {
        // Arrange
        Guid nonExistentGameId = Guid.NewGuid();

        // Act
        GameDto? result = await _gameService.GetGameAsync(nonExistentGameId, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetGameAsync_ReturnsGame_WhenGameExists()
    {
        // Arrange
        Game game = new Game
        {
            Id = Guid.NewGuid(),
            RulesetId = "test-ruleset",
            ScenarioId = "test-scenario",
            PlayerId = "test-player",
            CreatedAt = DateTime.UtcNow
        };
        _context.Games.Add(game);

        Message message = new Message
        {
            GameId = game.Id,
            Text = "Test message",
            CreatedAt = DateTime.UtcNow
        };
        _context.Messages.Add(message);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _context.ChangeTracker.Clear();

        // Act
        GameDto? result = await _gameService.GetGameAsync(game.Id, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.GameId.ShouldBe(game.Id);
        result.Ruleset.Id.ShouldBe("test-ruleset");
        result.Scenario.Id.ShouldBe("test-scenario");
        result.Player.Id.ShouldBe("test-player");
        result.Messages.ShouldHaveSingleItem();
        result.Messages[0].Text.ShouldBe("Test message");
    }

    [Fact]
    public async Task GetGameAsync_ReturnsMessagesInOrder()
    {
        // Arrange
        Game game = new Game
        {
            Id = Guid.NewGuid(),
            RulesetId = "test-ruleset",
            ScenarioId = "test-scenario",
            PlayerId = "test-player",
            CreatedAt = DateTime.UtcNow
        };
        _context.Games.Add(game);

        Message message1 = new Message
        {
            GameId = game.Id,
            Text = "First message",
            CreatedAt = DateTime.UtcNow
        };
        Message message2 = new Message
        {
            GameId = game.Id,
            Text = "Second message",
            CreatedAt = DateTime.UtcNow.AddSeconds(1)
        };
        Message message3 = new Message
        {
            GameId = game.Id,
            Text = "Third message",
            CreatedAt = DateTime.UtcNow.AddSeconds(2)
        };
        _context.Messages.AddRange(message1, message2, message3);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _context.ChangeTracker.Clear();

        // Act
        GameDto? result = await _gameService.GetGameAsync(game.Id, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result!.Messages.ShouldNotBeNull();
        result.Messages.Length.ShouldBe(3);
        result.Messages[0].Text.ShouldBe("First message");
        result.Messages[1].Text.ShouldBe("Second message");
        result.Messages[2].Text.ShouldBe("Third message");
    }

    [Fact]
    public async Task CreateGameAsync_ThrowsException_WhenPlayerDoesNotExist()
    {
        // Arrange
        string scenarioId = "test-scenario";
        string playerId = "nonexistent-player";

        // Act & Assert
        var exception = await Should.ThrowAsync<ArgumentException>(
            async () => await _gameService.CreateGameAsync(scenarioId, playerId, TestContext.Current.CancellationToken)
        );
        exception.Message.ShouldContain("Player 'nonexistent-player' does not exist");
    }

    [Fact]
    public async Task CreateGameAsync_ThrowsException_WhenScenarioDoesNotExist()
    {
        // Arrange
        string scenarioId = "nonexistent-scenario";
        string playerId = "test-player";

        // Act & Assert
        var exception = await Should.ThrowAsync<ArgumentException>(
            async () => await _gameService.CreateGameAsync(scenarioId, playerId, TestContext.Current.CancellationToken)
        );
        exception.Message.ShouldContain("Scenario 'nonexistent-scenario' does not exist");
    }

    [Fact]
    public async Task CreateGameAsync_ThrowsException_WhenPlayerAndScenarioRulesetMismatch()
    {
        // Arrange
        _context.Rulesets.Add(new Ruleset { Id = "different-ruleset", Name = "Different Ruleset" });
        _context.Players.Add(new Player { Id = "player-different-ruleset", RulesetId = "different-ruleset", Name = "Unspecified" });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        string scenarioId = "test-scenario";
        string playerId = "player-different-ruleset";

        // Act & Assert
        var exception = await Should.ThrowAsync<ArgumentException>(
            async () => await _gameService.CreateGameAsync(scenarioId, playerId, TestContext.Current.CancellationToken)
        );
        exception.Message.ShouldContain("uses ruleset 'different-ruleset'");
        exception.Message.ShouldContain("uses ruleset 'test-ruleset'");
    }

    // Mock implementations for testing
    private class MockChatService : IChatService
    {
        public const string TestInitialMessage = "Welcome to the test adventure!";

        public Task<JaimesChatResponse> ProcessChatMessageAsync(GameDto game, string message, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new JaimesChatResponse
            {
                Messages = [new MessageResponse
                {
                    Text = "Test response",
                    Participant = ChatParticipant.GameMaster,
                    PlayerId = null,
                    ParticipantName = "Game Master",
                    CreatedAt = DateTime.UtcNow
                }],
                ThreadJson = "{}"
            });
        }

        public Task<InitialMessageResponse> GenerateInitialMessageAsync(GenerateInitialMessageRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new InitialMessageResponse
            {
                Message = TestInitialMessage,
                ThreadJson = "{\"thread\":\"test\"}"
            });
        }
    }

    private class MockChatHistoryService : IChatHistoryService
    {
        private readonly Dictionary<Guid, string> _threads = new();

        public Task<string?> GetMostRecentThreadJsonAsync(Guid gameId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_threads.TryGetValue(gameId, out string? value) ? value : null);
        }

        public Task<Guid> SaveThreadJsonAsync(Guid gameId, string threadJson, int? messageId = null, CancellationToken cancellationToken = default)
        {
            _threads[gameId] = threadJson;
            return Task.FromResult(Guid.NewGuid());
        }
    }
}
