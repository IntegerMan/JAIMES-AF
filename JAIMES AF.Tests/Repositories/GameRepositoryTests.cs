using MattEland.Jaimes.Repositories.Entities;
using Shouldly;

namespace MattEland.Jaimes.Tests.Repositories;

public class GameRepositoryTests : RepositoryTestBase
{
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
        Context.Games.Add(game);
        await Context.SaveChangesAsync();

        var retrievedGame = await Context.Games.FindAsync(game.Id);

        // Assert
        retrievedGame.ShouldNotBeNull();
        retrievedGame.Id.ShouldBe(game.Id);
        retrievedGame.RulesetId.ShouldBe("test-ruleset");
        retrievedGame.ScenarioId.ShouldBe("test-scenario");
        retrievedGame.PlayerId.ShouldBe("test-player");
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
        Context.Games.Add(game);

        var message = new Message
        {
            GameId = game.Id,
            Text = "Test message",
            CreatedAt = DateTime.UtcNow
        };
        Context.Messages.Add(message);
        await Context.SaveChangesAsync();

        var messageId = message.Id;

        // Act
        Context.Games.Remove(game);
        await Context.SaveChangesAsync();

        // Assert
        var deletedMessage = await Context.Messages.FindAsync(messageId);
        deletedMessage.ShouldBeNull();
    }
}
