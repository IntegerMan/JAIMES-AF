using Microsoft.EntityFrameworkCore;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.Services;

namespace MattEland.Jaimes.Tests;

public class GameServiceTests : IAsyncLifetime
{
    private JaimesDbContext _context = null!;
    private GameService _gameService = null!;

    public async ValueTask InitializeAsync()
    {
        // Create an in-memory database for testing
        var options = new DbContextOptionsBuilder<JaimesDbContext>()
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
        var rulesetId = "test-ruleset";
        var scenarioId = "test-scenario";
        var playerId = "test-player";

        // Act
        var gameDto = await _gameService.CreateGameAsync(rulesetId, scenarioId, playerId);

        // Assert
        Assert.NotEqual(Guid.Empty, gameDto.GameId);
        Assert.Equal(rulesetId, gameDto.RulesetId);
        Assert.Equal(scenarioId, gameDto.ScenarioId);
        Assert.Equal(playerId, gameDto.PlayerId);
        Assert.Single(gameDto.Messages);
        Assert.Equal("Hello World", gameDto.Messages[0].Text);

        // Verify game is in database
        var gameInDb = await _context.Games.FindAsync(gameDto.GameId);
        Assert.NotNull(gameInDb);
        Assert.Equal(rulesetId, gameInDb.RulesetId);

        // Verify message is in database
        var messageInDb = await _context.Messages.FirstOrDefaultAsync(m => m.GameId == gameDto.GameId);
        Assert.NotNull(messageInDb);
        Assert.Equal("Hello World", messageInDb.Text);
    }

    [Fact]
    public async Task GetGameAsync_ReturnsNull_WhenGameDoesNotExist()
    {
        // Arrange
        var nonExistentGameId = Guid.NewGuid();

        // Act
        var result = await _gameService.GetGameAsync(nonExistentGameId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetGameAsync_ReturnsGame_WhenGameExists()
    {
        // Arrange
        var game = new Game
        {
            Id = Guid.NewGuid(),
            RulesetId = "test-ruleset",
            ScenarioId = "test-scenario",
            PlayerId = "test-player",
            CreatedAt = DateTime.UtcNow
        };
        _context.Games.Add(game);

        var message = new Message
        {
            GameId = game.Id,
            Text = "Test message",
            CreatedAt = DateTime.UtcNow
        };
        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        // Act
        var result = await _gameService.GetGameAsync(game.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(game.Id, result.GameId);
        Assert.Equal("test-ruleset", result.RulesetId);
        Assert.Equal("test-scenario", result.ScenarioId);
        Assert.Equal("test-player", result.PlayerId);
        Assert.Single(result.Messages);
        Assert.Equal("Test message", result.Messages[0].Text);
    }

    [Fact]
    public async Task GetGameAsync_ReturnsMessagesInOrder()
    {
        // Arrange
        var game = new Game
        {
            Id = Guid.NewGuid(),
            RulesetId = "test-ruleset",
            ScenarioId = "test-scenario",
            PlayerId = "test-player",
            CreatedAt = DateTime.UtcNow
        };
        _context.Games.Add(game);

        var message1 = new Message
        {
            GameId = game.Id,
            Text = "First message",
            CreatedAt = DateTime.UtcNow
        };
        var message2 = new Message
        {
            GameId = game.Id,
            Text = "Second message",
            CreatedAt = DateTime.UtcNow.AddSeconds(1)
        };
        var message3 = new Message
        {
            GameId = game.Id,
            Text = "Third message",
            CreatedAt = DateTime.UtcNow.AddSeconds(2)
        };
        _context.Messages.AddRange(message1, message2, message3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _gameService.GetGameAsync(game.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Messages.Length);
        Assert.Equal("First message", result.Messages[0].Text);
        Assert.Equal("Second message", result.Messages[1].Text);
        Assert.Equal("Third message", result.Messages[2].Text);
    }
}
