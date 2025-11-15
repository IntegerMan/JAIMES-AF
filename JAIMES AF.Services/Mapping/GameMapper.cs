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
            RulesetId = game.RulesetId,
            RulesetName = game.Ruleset?.Name ?? game.RulesetId,
            ScenarioId = game.ScenarioId,
            ScenarioName = game.Scenario?.Name ?? game.ScenarioId,
            PlayerId = game.PlayerId,
            PlayerName = game.Player?.Name ?? game.PlayerId,
            Messages = game.Messages?
                .OrderBy(m => m.CreatedAt)
                .Select(m => m.ToDto())
                .ToArray(),
            SystemPrompt = game.Scenario?.SystemPrompt ?? string.Empty
        };
    }

    public static partial GameDto[] ToDto(this IEnumerable<Game> games);
}
