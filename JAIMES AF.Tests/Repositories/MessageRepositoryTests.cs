using Microsoft.EntityFrameworkCore;
using MattEland.Jaimes.Repositories.Entities;
using Shouldly;

namespace MattEland.Jaimes.Tests.Repositories;

public class MessageRepositoryTests : RepositoryTestBase
{
    [Fact]
    public async Task CanCreateAndRetrieveMessage()
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
        Context.Games.Add(game);
        await Context.SaveChangesAsync();

        Message message = new Message
        {
            GameId = game.Id,
            Text = "Test message",
            CreatedAt = DateTime.UtcNow
        };

        // Act
        Context.Messages.Add(message);
        await Context.SaveChangesAsync();

        Message? retrievedMessage = await Context.Messages.FindAsync(message.Id);

        // Assert
        retrievedMessage.ShouldNotBeNull();
        retrievedMessage.GameId.ShouldBe(game.Id);
        retrievedMessage.Text.ShouldBe("Test message");
    }

    [Fact]
    public async Task GameIncludesMessages()
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
        Context.Games.Add(game);

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
        Context.Messages.AddRange(message1, message2);
        await Context.SaveChangesAsync();

        // Act
        Game? retrievedGame = await Context.Games
            .Include(g => g.Messages)
            .FirstOrDefaultAsync(g => g.Id == game.Id);

        // Assert
        retrievedGame.ShouldNotBeNull();
        retrievedGame.Messages.Count.ShouldBe(2);
        retrievedGame.Messages.ShouldContain(m => m.Text == "First message");
        retrievedGame.Messages.ShouldContain(m => m.Text == "Second message");
    }
}
