using Microsoft.EntityFrameworkCore;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;

namespace MattEland.Jaimes.Tests;

public class RepositoryIntegrationTests : IAsyncLifetime
{
    private JaimesDbContext _context = null!;

    public async ValueTask InitializeAsync()
    {
        // Create an in-memory database for testing
        var options = new DbContextOptionsBuilder<JaimesDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _context = new JaimesDbContext(options);
        await _context.Database.OpenConnectionAsync();
        await _context.Database.EnsureCreatedAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _context.Database.CloseConnectionAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task CanCreateAndRetrieveGame()
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

        // Act
        _context.Games.Add(game);
        await _context.SaveChangesAsync();

        var retrievedGame = await _context.Games.FindAsync(game.Id);

        // Assert
        Assert.NotNull(retrievedGame);
        Assert.Equal(game.Id, retrievedGame.Id);
        Assert.Equal("test-ruleset", retrievedGame.RulesetId);
        Assert.Equal("test-scenario", retrievedGame.ScenarioId);
        Assert.Equal("test-player", retrievedGame.PlayerId);
    }

    [Fact]
    public async Task CanCreateAndRetrieveMessage()
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
        await _context.SaveChangesAsync();

        var message = new Message
        {
            GameId = game.Id,
            Text = "Test message",
            CreatedAt = DateTime.UtcNow
        };

        // Act
        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        var retrievedMessage = await _context.Messages.FindAsync(message.Id);

        // Assert
        Assert.NotNull(retrievedMessage);
        Assert.Equal(game.Id, retrievedMessage.GameId);
        Assert.Equal("Test message", retrievedMessage.Text);
    }

    [Fact]
    public async Task GameIncludesMessages()
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
        _context.Messages.AddRange(message1, message2);
        await _context.SaveChangesAsync();

        // Act
        var retrievedGame = await _context.Games
            .Include(g => g.Messages)
            .FirstOrDefaultAsync(g => g.Id == game.Id);

        // Assert
        Assert.NotNull(retrievedGame);
        Assert.Equal(2, retrievedGame.Messages.Count);
        Assert.Contains(retrievedGame.Messages, m => m.Text == "First message");
        Assert.Contains(retrievedGame.Messages, m => m.Text == "Second message");
    }

    [Fact]
    public async Task DeletingGameCascadesDeleteToMessages()
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

        var messageId = message.Id;

        // Act
        _context.Games.Remove(game);
        await _context.SaveChangesAsync();

        // Assert
        var deletedMessage = await _context.Messages.FindAsync(messageId);
        Assert.Null(deletedMessage);
    }
}
