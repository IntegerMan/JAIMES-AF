using System.Linq;
using System.Collections.Generic;
using MattEland.Jaimes.Domain;
using MattEland.Jaimes.Repositories.Entities;

namespace MattEland.Jaimes.ServiceLayer.Mapping;

// Manual mapper for Game because DTO property names don't match EF model (Id -> GameId)
public static class GameMapper
{
    public static GameDto ToDto(this Game? game)
    {
        if (game == null)
            return null!;

        return new GameDto
        {
            GameId = game.Id,
            RulesetId = game.RulesetId,
            RulesetName = game.Ruleset!.Name,
            ScenarioId = game.ScenarioId,
            ScenarioName = game.Scenario!.Name,
            PlayerId = game.PlayerId,
            PlayerName = game.Player!.Name,
            Messages = game.Messages?
                            .OrderBy(m => m.CreatedAt)
                            .Select(m => new MessageDto(m.Text, m.PlayerId, m.Player?.Name ?? "Game Master", m.CreatedAt))
                            .ToArray()
        };
    }

    public static GameDto[] ToDto(this IEnumerable<Game>? games)
    {
        return games?.Select(g => g.ToDto()).ToArray() ?? [];
    }
}
