using MattEland.Jaimes.Domain;
using MattEland.Jaimes.Repositories.Entities;
using Riok.Mapperly.Abstractions;

namespace MattEland.Jaimes.ServiceLayer.Mapping;

[Mapper]
public static partial class GameMapper
{
    [UserMapping]
    public static GameDto ToDto(this Game game)
    {
        return new GameDto
        {
            GameId = game.Id,
            Ruleset = game.Ruleset?.ToDto() ?? new RulesetDto
            {
                Id = game.RulesetId,
                Name = game.RulesetId
            },
            Scenario = game.Scenario?.ToDto() ?? new ScenarioDto
            {
                Id = game.ScenarioId,
                RulesetId = game.RulesetId,
                Name = game.ScenarioId,
                SystemPrompt = string.Empty,
                NewGameInstructions = string.Empty
            },
            Player = game.Player?.ToDto() ?? new PlayerDto
            {
                Id = game.PlayerId,
                RulesetId = game.RulesetId,
                Name = game.PlayerId
            },
            Messages = game.Messages?
                .OrderBy(m => m.Id)
                .Select(m => m.ToDto())
                .ToArray()
        };
    }

    public static partial GameDto[] ToDto(this IEnumerable<Game> games);
}
