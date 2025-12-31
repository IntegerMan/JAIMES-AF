namespace MattEland.Jaimes.Tests.Repositories;

public class GameRepositoryTests : RepositoryTestBase
{
    [Fact]
    public async Task CanCreateAndRetrieveGame()
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

        // Act
        Context.Games.Add(game);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Game? retrievedGame = await Context.Games.FindAsync([game.Id], TestContext.Current.CancellationToken);

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
        Game game = new()
        {
            Id = Guid.NewGuid(),
            RulesetId = "test-ruleset",
            ScenarioId = "test-scenario",
            PlayerId = "test-player",
            CreatedAt = DateTime.UtcNow
        };
        Context.Games.Add(game);

        Message message = new()
        {
            GameId = game.Id,
            Text = "Test message",
            CreatedAt = DateTime.UtcNow,
            AgentId = "test-agent",
            InstructionVersionId = 1
        };
        Context.Messages.Add(message);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        int messageId = message.Id;

        // Act
        Context.Games.Remove(game);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        Message? deletedMessage = await Context.Messages.FindAsync([messageId], TestContext.Current.CancellationToken);
        deletedMessage.ShouldBeNull();
    }
}