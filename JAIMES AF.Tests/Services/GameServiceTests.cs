using Microsoft.EntityFrameworkCore;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceLayer.Services;
using MattEland.Jaimes.Services.Models;
using Shouldly;

namespace MattEland.Jaimes.Tests.Services;

public class GameServiceTests : IAsyncLifetime
{
    private JaimesDbContext _context = null!;
    private GameService _gameService = null!;

    public async ValueTask InitializeAsync()
    {
        // Create an in-memory database for testing
        DbContextOptions<JaimesDbContext> options = new DbContextOptionsBuilder<JaimesDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _context = new JaimesDbContext(options);
        await _context.Database.OpenConnectionAsync();
        await _context.Database.EnsureCreatedAsync();

        _gameService = new GameService(_context);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.Database.CloseConnectionAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task CreateGameAsync_CreatesGameWithInitialMessage()
    {
        // Arrange
        string rulesetId = "test-ruleset";
        string scenarioId = "test-scenario";
        string playerId = "test-player";

        // Act
        GameDto gameDto = await _gameService.CreateGameAsync(rulesetId, scenarioId, playerId);

        // Assert
        gameDto.GameId.ShouldNotBe(Guid.Empty);
        gameDto.RulesetId.ShouldBe(rulesetId);
        gameDto.ScenarioId.ShouldBe(scenarioId);
        gameDto.PlayerId.ShouldBe(playerId);
        gameDto.Messages.ShouldHaveSingleItem();
        gameDto.Messages[0].Text.ShouldBe("Hello World");

        // Verify game is in database
        Game? gameInDb = await _context.Games.FindAsync(gameDto.GameId);
        gameInDb.ShouldNotBeNull();
        gameInDb.RulesetId.ShouldBe(rulesetId);

        // Verify message is in database
        Message? messageInDb = await _context.Messages.FirstOrDefaultAsync(m => m.GameId == gameDto.GameId);
        messageInDb.ShouldNotBeNull();
        messageInDb.Text.ShouldBe("Hello World");
    }

    [Fact]
    public async Task GetGameAsync_ReturnsNull_WhenGameDoesNotExist()
    {
        // Arrange
        Guid nonExistentGameId = Guid.NewGuid();

        // Act
        GameDto? result = await _gameService.GetGameAsync(nonExistentGameId);

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
        await _context.SaveChangesAsync();

        // Act
        GameDto? result = await _gameService.GetGameAsync(game.Id);

        // Assert
        result.ShouldNotBeNull();
        result.GameId.ShouldBe(game.Id);
        result.RulesetId.ShouldBe("test-ruleset");
        result.ScenarioId.ShouldBe("test-scenario");
        result.PlayerId.ShouldBe("test-player");
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
        await _context.SaveChangesAsync();

        // Act
        GameDto? result = await _gameService.GetGameAsync(game.Id);

        // Assert
        result.ShouldNotBeNull();
        result.Messages.Length.ShouldBe(3);
        result.Messages[0].Text.ShouldBe("First message");
        result.Messages[1].Text.ShouldBe("Second message");
        result.Messages[2].Text.ShouldBe("Third message");
    }
}
