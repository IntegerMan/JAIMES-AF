using System.Text.Json;
using MattEland.Jaimes.Agents.Services;

namespace MattEland.Jaimes.Tests.Services;

public class EntityFrameworkMemoryProviderTests : IAsyncLifetime
{
    private JaimesDbContext _context = null!;
    private IDbContextFactory<JaimesDbContext> _contextFactory = null!;
    private EntityFrameworkMemoryProvider _memoryProvider = null!;
    private ILogger<EntityFrameworkMemoryProvider> _logger = null!;
    private MockAgent _mockAgent = null!;

    public async ValueTask InitializeAsync()
    {
        // Create an in-memory database for testing
        DbContextOptions<JaimesDbContext> options = new DbContextOptionsBuilder<JaimesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new JaimesDbContext(options);
        await _context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        // Add test data
        _context.Rulesets.Add(new Ruleset { Id = "test-ruleset", Name = "Test Ruleset" });
        _context.Players.Add(new Player { Id = "test-player", RulesetId = "test-ruleset", Name = "Test Player" });
        _context.Scenarios.Add(new Scenario
        {
            Id = "test-scenario",
            RulesetId = "test-ruleset",
            Name = "Test Scenario",
            SystemPrompt = "Test System Prompt",
            NewGameInstructions = "Test Instructions"
        });
        _context.Games.Add(new Game
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            RulesetId = "test-ruleset",
            ScenarioId = "test-scenario",
            PlayerId = "test-player",
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Create factory and services
        _contextFactory = new TestDbContextFactory(options);
        _logger = new MockLogger<EntityFrameworkMemoryProvider>();
        _memoryProvider = new EntityFrameworkMemoryProvider(_contextFactory, _logger);
        _mockAgent = new MockAgent();
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task LoadThreadAsync_ReturnsNewThread_WhenNoHistoryExists()
    {
        // Arrange
        Guid gameId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        // Act
        AgentThread thread = await _memoryProvider.LoadThreadAsync(gameId, _mockAgent, TestContext.Current.CancellationToken);

        // Assert
        thread.ShouldNotBeNull();
        _mockAgent.GetNewThreadCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task LoadThreadAsync_ReturnsDeserializedThread_WhenHistoryExists()
    {
        // Arrange
        Guid gameId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        string testThreadJson = "{\"messages\":[{\"role\":\"user\",\"text\":\"Hello\"}]}";

        ChatHistory history = new()
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            ThreadJson = testThreadJson,
            CreatedAt = DateTime.UtcNow
        };
        _context.ChatHistories.Add(history);

        Game? game = await _context.Games.FindAsync([gameId], TestContext.Current.CancellationToken);
        game.ShouldNotBeNull();
        game.MostRecentHistoryId = history.Id;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        AgentThread thread = await _memoryProvider.LoadThreadAsync(gameId, _mockAgent, TestContext.Current.CancellationToken);

        // Assert
        thread.ShouldNotBeNull();
        _mockAgent.DeserializeThreadCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task SaveConversationAsync_SavesMessagesAndThread()
    {
        // Arrange
        Guid gameId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        string playerId = "test-player";
        ChatMessage userMessage = new(ChatRole.User, "Hello, AI!");
        List<ChatMessage> assistantMessages = new()
        {
            new ChatMessage(ChatRole.Assistant, "Hello, human!")
        };
        AgentThread thread = _mockAgent.GetNewThread();

        // Act
        await _memoryProvider.SaveConversationAsync(
            gameId,
            playerId,
            userMessage,
            assistantMessages,
            thread,
            TestContext.Current.CancellationToken);

        // Assert
        List<Message> messages = await _context.Messages
            .Where(m => m.GameId == gameId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(TestContext.Current.CancellationToken);

        messages.Count.ShouldBe(2);
        messages[0].Text.ShouldBe("Hello, AI!");
        messages[0].PlayerId.ShouldBe(playerId);
        messages[1].Text.ShouldBe("Hello, human!");
        messages[1].PlayerId.ShouldBeNull(); // Game Master

        // Verify thread was saved
        Game? game = await _context.Games
            .Include(g => g.MostRecentHistory)
            .FirstOrDefaultAsync(g => g.Id == gameId, TestContext.Current.CancellationToken);
        game.ShouldNotBeNull();
        game.MostRecentHistory.ShouldNotBeNull();
        game.MostRecentHistory.ThreadJson.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task SaveConversationAsync_HandlesNullUserMessage()
    {
        // Arrange
        Guid gameId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        string playerId = "test-player";
        List<ChatMessage> assistantMessages = new()
        {
            new ChatMessage(ChatRole.Assistant, "Hello!")
        };
        AgentThread thread = _mockAgent.GetNewThread();

        // Act
        await _memoryProvider.SaveConversationAsync(
            gameId,
            playerId,
            null,
            assistantMessages,
            thread,
            TestContext.Current.CancellationToken);

        // Assert
        List<Message> messages = await _context.Messages
            .Where(m => m.GameId == gameId)
            .ToListAsync(TestContext.Current.CancellationToken);

        messages.Count.ShouldBe(1);
        messages[0].Text.ShouldBe("Hello!");
        messages[0].PlayerId.ShouldBeNull();
    }

    [Fact]
    public async Task GetConversationHistoryAsync_ReturnsMessagesInOrder()
    {
        // Arrange
        Guid gameId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        DateTime baseTime = DateTime.UtcNow;

        _context.Messages.AddRange(
            new Message
            {
                GameId = gameId,
                Text = "First message",
                PlayerId = "test-player",
                CreatedAt = baseTime
            },
            new Message
            {
                GameId = gameId,
                Text = "Second message",
                PlayerId = null,
                CreatedAt = baseTime.AddSeconds(1)
            },
            new Message
            {
                GameId = gameId,
                Text = "Third message",
                PlayerId = "test-player",
                CreatedAt = baseTime.AddSeconds(2)
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        IEnumerable<ChatMessage> history = await _memoryProvider.GetConversationHistoryAsync(
            gameId,
            TestContext.Current.CancellationToken);

        // Assert
        List<ChatMessage> historyList = history.ToList();
        historyList.Count.ShouldBe(3);
        historyList[0].Text.ShouldBe("First message");
        historyList[0].Role.ShouldBe(ChatRole.User);
        historyList[1].Text.ShouldBe("Second message");
        historyList[1].Role.ShouldBe(ChatRole.Assistant);
        historyList[2].Text.ShouldBe("Third message");
        historyList[2].Role.ShouldBe(ChatRole.User);
    }

    [Fact]
    public async Task GetConversationHistoryAsync_ReturnsEmptyForNonExistentGame()
    {
        // Arrange
        Guid nonExistentGameId = Guid.NewGuid();

        // Act
        IEnumerable<ChatMessage> history = await _memoryProvider.GetConversationHistoryAsync(
            nonExistentGameId,
            TestContext.Current.CancellationToken);

        // Assert
        history.ShouldBeEmpty();
    }

    private class MockAgent : AIAgent
    {
        public bool GetNewThreadCalled { get; private set; }
        public bool DeserializeThreadCalled { get; private set; }

        public override AgentThread GetNewThread()
        {
            GetNewThreadCalled = true;
            return new MockAgentThread();
        }

        public override AgentThread DeserializeThread(JsonElement jsonElement, JsonSerializerOptions? options = null)
        {
            DeserializeThreadCalled = true;
            return new MockAgentThread();
        }

        public override Task<AgentRunResponse> RunAsync(
            IEnumerable<ChatMessage> messages,
            AgentThread? thread = null,
            AgentRunOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AgentRunResponse
            {
                Messages = new[] { new ChatMessage(ChatRole.Assistant, "Test response") }
            });
        }

        public override IAsyncEnumerable<AgentRunResponseUpdate> RunStreamingAsync(
            IEnumerable<ChatMessage> messages,
            AgentThread? thread = null,
            AgentRunOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }

    private class MockAgentThread : AgentThread
    {
        public override JsonElement Serialize(JsonSerializerOptions? options = null)
        {
            string json = "{\"test\":\"thread\"}";
            return JsonSerializer.Deserialize<JsonElement>(json);
        }
    }

    private class MockLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    private class TestDbContextFactory(DbContextOptions<JaimesDbContext> options) : IDbContextFactory<JaimesDbContext>
    {
        public JaimesDbContext CreateDbContext()
        {
            return new JaimesDbContext(options);
        }

        public async Task<JaimesDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return await Task.FromResult(new JaimesDbContext(options));
        }
    }
}
