using MattEland.Jaimes.Tools;

namespace MattEland.Jaimes.Tests.Tools;

public class PlayerInfoToolTests
{
    [Theory]
    [InlineData("Test Player", "test-player-id", null, false)]
    [InlineData("Test Player", "test-player-id", "", false)]
    [InlineData("Test Player", "test-player-id", "A brave warrior", true)]
    [InlineData("Gandalf", "wizard-001", "A powerful wizard", true)]
    [InlineData("Aragorn", "ranger-001", null, false)]
    public void GetPlayerInfo_ReturnsCorrectInformation(string playerName,
        string playerId,
        string? playerDescription,
        bool shouldContainDescription)
    {
        // Arrange
        GameDto game = CreateGameDto(playerName, playerId, playerDescription);
        PlayerInfoTool tool = new(game);

        // Act
        string result = tool.GetPlayerInfo();

        // Assert
        result.ShouldContain(playerName);
        result.ShouldContain(playerId);

        if (shouldContainDescription && !string.IsNullOrWhiteSpace(playerDescription))
        {
            result.ShouldContain("Player Description");
            result.ShouldContain(playerDescription);
        }
        else
        {
            result.ShouldNotContain("Player Description");
        }
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenGameIsNull()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new PlayerInfoTool(null!));
    }

    private static GameDto CreateGameDto(string playerName, string playerId, string? playerDescription)
    {
        return new GameDto
        {
            GameId = Guid.NewGuid(),
            Player = new PlayerDto
            {
                Id = playerId,
                Name = playerName,
                Description = playerDescription,
                RulesetId = "test-ruleset"
            },
            Scenario = new ScenarioDto
            {
                Id = "test-scenario",
                Name = "Test Scenario",
                SystemPrompt = "Test system prompt",
                NewGameInstructions = "Test instructions",
                RulesetId = "test-ruleset"
            },
            Ruleset = new RulesetDto
            {
                Id = "test-ruleset",
                Name = "Test Ruleset"
            },
            Messages = null
        };
    }
}