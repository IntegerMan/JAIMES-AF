using System.Linq;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.Services.Models;
using System.Collections.Generic;

namespace MattEland.Jaimes.ServiceLayer.Services;

// Manual mapper for Game because DTO property names don't match EF model (Id -> GameId)
public static class GameMapper
{
 public static GameDto ToDto(this Game game)
 {
 if (game == null) return null!;

 return new GameDto
 {
 GameId = game.Id,
 RulesetId = game.RulesetId,
 ScenarioId = game.ScenarioId,
 PlayerId = game.PlayerId,
 Messages = game.Messages?
 .OrderBy(m => m.CreatedAt)
 .Select(m => new MessageDto(m.Text))
 .ToArray()
 };
 }

 public static GameDto[] ToDto(this IEnumerable<Game> games)
 {
 return games?.Select(g => g.ToDto()).ToArray() ?? System.Array.Empty<GameDto>();
 }
}
