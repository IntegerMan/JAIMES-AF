using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MattEland.Jaimes.ServiceLayer.Services;
using MattEland.Jaimes.ServiceDefaults;

namespace MattEland.Jaimes.Tests.Services;

public class GameServiceTests : IAsyncLifetime
{
    private JaimesDbContext _context = null!;
    private IDbContextFactory<JaimesDbContext> _contextFactory = null!;
    private GameService _gameService = null!;
    private MockChatService _mockChatService = null!;
    private MockChatHistoryService _mockChatHistoryService = null!;

    public async ValueTask InitializeAsync()
    {
        // Create an in-memory database for testing
        DbContextOptions<JaimesDbContext> options = new DbContextOptionsBuilder<JaimesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new JaimesDbContext(options);
        await _context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        // Add test data for validation
        _context.Rulesets.Add(new Ruleset { Id = "test-ruleset", Name = "Test Ruleset" });
        _context.Players.Add(new Player { Id = "test-player", RulesetId = "test-ruleset", Name = "Unspecified" });
        _context.Scenarios.Add(new Scenario
        {
            Id = "test-scenario",
            RulesetId = "test-ruleset",
            Name = "Unspecified",
            SystemPrompt = "Test System Prompt",
            InitialGreeting = null
        });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Create a factory that returns the same context (for testing)
        _contextFactory = new TestDbContextFactory(options);
        _mockChatService = new MockChatService();
        _mockChatHistoryService = new MockChatHistoryService();

        // Use a real chat client with Ollama provider (no API keys required for testing)
        // This allows the tests to create real agents that support the reflection method
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                {"TextGenerationModel:Provider", "Ollama"}
            })
            .Build();
        services.AddLogging();
        services.AddHttpClient();
        services.AddChatClient(configuration, "TextGenerationModel", null, null);
        ServiceProvider serviceProvider = services.BuildServiceProvider();
        IChatClient chatClient = serviceProvider.GetRequiredService<IChatClient>();

        ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _gameService = new GameService(_contextFactory, _mockChatHistoryService, chatClient, loggerFactory);
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
        GameDto gameDto =
            await _gameService.CreateGameAsync(scenarioId, playerId, TestContext.Current.CancellationToken);

        // Assert
        gameDto.GameId.ShouldNotBe(Guid.Empty);
        gameDto.Ruleset.Id.ShouldBe("test-ruleset");
        gameDto.Scenario.Id.ShouldBe(scenarioId);
        gameDto.Player.Id.ShouldBe(playerId);
        gameDto.Messages.ShouldHaveSingleItem();
        // The message should be the InitialGreeting from the scenario, or "Welcome to the adventure!" if not set
        // Since test scenario doesn't have InitialGreeting, it should use the fallback
        gameDto.Messages[0].Text.ShouldBe("Welcome to the adventure!");
        gameDto.Messages[0].ParticipantName.ShouldBe("Game Master");
        gameDto.Messages[0].Id.ShouldBeGreaterThan(0);

        // Verify game is in database
        Game? gameInDb = await _context.Games.FindAsync([gameDto.GameId], TestContext.Current.CancellationToken);
        gameInDb.ShouldNotBeNull();
        gameInDb.RulesetId.ShouldBe("test-ruleset");

        // Verify message is in database
        Message? messageInDb = await _context.Messages.FirstOrDefaultAsync(m => m.GameId == gameDto.GameId,
            TestContext.Current.CancellationToken);
        messageInDb.ShouldNotBeNull();
        messageInDb.Text.ShouldBe("Welcome to the adventure!");
        messageInDb.PlayerId.ShouldBeNull(); // System message
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
        Game game = new()
        {
            Id = Guid.NewGuid(),
            RulesetId = "test-ruleset",
            ScenarioId = "test-scenario",
            PlayerId = "test-player",
            CreatedAt = DateTime.UtcNow
        };
        _context.Games.Add(game);

        Message message = new()
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
        result.Messages[0].Id.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task GetGameAsync_ReturnsMessagesInOrder()
    {
        // Arrange
        Game game = new()
        {
            Id = Guid.NewGuid(),
            RulesetId = "test-ruleset",
            ScenarioId = "test-scenario",
            PlayerId = "test-player",
            CreatedAt = DateTime.UtcNow
        };
        _context.Games.Add(game);

        Message message1 = new()
        {
            GameId = game.Id,
            Text = "First message",
            CreatedAt = DateTime.UtcNow
        };
        Message message2 = new()
        {
            GameId = game.Id,
            Text = "Second message",
            CreatedAt = DateTime.UtcNow.AddSeconds(1)
        };
        Message message3 = new()
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
        result.Messages[0].Id.ShouldBeGreaterThan(0);
        result.Messages[1].Text.ShouldBe("Second message");
        result.Messages[1].Id.ShouldBeGreaterThan(result.Messages[0].Id);
        result.Messages[2].Text.ShouldBe("Third message");
        result.Messages[2].Id.ShouldBeGreaterThan(result.Messages[1].Id);
    }

    [Fact]
    public async Task GetGameAsync_ReturnsMessagesOrderedById_WhenCreatedAtIsSame()
    {
        // Arrange - This test verifies that Id ordering works even when CreatedAt timestamps are identical
        Game game = new()
        {
            Id = Guid.NewGuid(),
            RulesetId = "test-ruleset",
            ScenarioId = "test-scenario",
            PlayerId = "test-player",
            CreatedAt = DateTime.UtcNow
        };
        _context.Games.Add(game);

        DateTime sameTime = DateTime.UtcNow;
        Message message1 = new()
        {
            GameId = game.Id,
            Text = "First message",
            CreatedAt = sameTime
        };
        Message message2 = new()
        {
            GameId = game.Id,
            Text = "Second message",
            CreatedAt = sameTime
        };
        Message message3 = new()
        {
            GameId = game.Id,
            Text = "Third message",
            CreatedAt = sameTime
        };
        _context.Messages.AddRange(message1, message2, message3);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _context.ChangeTracker.Clear();

        // Act
        GameDto? result = await _gameService.GetGameAsync(game.Id, TestContext.Current.CancellationToken);

        // Assert - Messages should be ordered by Id, not CreatedAt
        result.ShouldNotBeNull();
        result!.Messages.ShouldNotBeNull();
        result.Messages.Length.ShouldBe(3);
        result.Messages[0].Id.ShouldBeLessThan(result.Messages[1].Id);
        result.Messages[1].Id.ShouldBeLessThan(result.Messages[2].Id);
        // Verify all have the same CreatedAt
        result.Messages[0].CreatedAt.ShouldBe(result.Messages[1].CreatedAt);
        result.Messages[1].CreatedAt.ShouldBe(result.Messages[2].CreatedAt);
    }

    [Fact]
    public async Task CreateGameAsync_ThrowsException_WhenPlayerDoesNotExist()
    {
        // Arrange
        string scenarioId = "test-scenario";
        string playerId = "nonexistent-player";

        // Act & Assert
        ArgumentException exception = await Should.ThrowAsync<ArgumentException>(async () =>
            await _gameService.CreateGameAsync(scenarioId, playerId, TestContext.Current.CancellationToken)
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
        ArgumentException exception = await Should.ThrowAsync<ArgumentException>(async () =>
            await _gameService.CreateGameAsync(scenarioId, playerId, TestContext.Current.CancellationToken)
        );
        exception.Message.ShouldContain("Scenario 'nonexistent-scenario' does not exist");
    }

    [Fact]
    public async Task CreateGameAsync_ThrowsException_WhenPlayerAndScenarioRulesetMismatch()
    {
        // Arrange
        _context.Rulesets.Add(new Ruleset { Id = "different-ruleset", Name = "Different Ruleset" });
        _context.Players.Add(new Player
        { Id = "player-different-ruleset", RulesetId = "different-ruleset", Name = "Unspecified" });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        string scenarioId = "test-scenario";
        string playerId = "player-different-ruleset";

        // Act & Assert
        ArgumentException exception = await Should.ThrowAsync<ArgumentException>(async () =>
            await _gameService.CreateGameAsync(scenarioId, playerId, TestContext.Current.CancellationToken)
        );
        exception.Message.ShouldContain("uses ruleset 'different-ruleset'");
        exception.Message.ShouldContain("uses ruleset 'test-ruleset'");
    }

    [Fact]
    public async Task CreateGameAsync_UsesInitialGreeting_WhenSet()
    {
        // Arrange
        string customGreeting = "We're about to get started on a solo adventure, but before we do, tell me a bit about your character and the style of adventure you'd like to have";
        _context.Scenarios.Add(new Scenario
        {
            Id = "scenario-with-greeting",
            RulesetId = "test-ruleset",
            Name = "Test Scenario",
            SystemPrompt = "Test System Prompt",
            InitialGreeting = customGreeting
        });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        GameDto gameDto = await _gameService.CreateGameAsync("scenario-with-greeting", "test-player", TestContext.Current.CancellationToken);

        // Assert
        gameDto.Messages.ShouldHaveSingleItem();
        gameDto.Messages[0].Text.ShouldBe(customGreeting);
        gameDto.Messages[0].ParticipantName.ShouldBe("Game Master");
    }


    // Test factory that returns the same context instance (for testing)
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

    // Mock implementations for testing
    private class MockChatService : IChatService
    {
        public const string TestInitialMessage = "Welcome to the test adventure!";

        public Task<InitialMessageResponse> GenerateInitialMessageAsync(GenerateInitialMessageRequest request,
            CancellationToken cancellationToken = default)
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

        public Task<Guid> SaveThreadJsonAsync(Guid gameId,
            string threadJson,
            int? messageId = null,
            CancellationToken cancellationToken = default)
        {
            _threads[gameId] = threadJson;
            return Task.FromResult(Guid.NewGuid());
        }
    }

    private class MockChatClient : IChatClient
    {
        public IReadOnlyList<ChatMessage>? History => throw new NotSupportedException();
        public void AddSystemMessage(string text) => throw new NotSupportedException();
        public void ClearHistory() => throw new NotSupportedException();
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}