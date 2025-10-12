using Microsoft.EntityFrameworkCore;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.Services.Models;

namespace MattEland.Jaimes.Services;

public class GameService(JaimesDbContext context) : IGameService
{
    public async Task<GameDto> CreateGameAsync(string rulesetId, string scenarioId, string playerId, CancellationToken cancellationToken = default)
    {
        Game game = new Game
        {
            Id = Guid.NewGuid(),
            RulesetId = rulesetId,
            ScenarioId = scenarioId,
            PlayerId = playerId,
            CreatedAt = DateTime.UtcNow
        };

        Message message = new Message
        {
            GameId = game.Id,
            Text = "Hello World",
            CreatedAt = DateTime.UtcNow
        };

        context.Games.Add(game);
        context.Messages.Add(message);
        await context.SaveChangesAsync(cancellationToken);

        return new GameDto
        {
            GameId = game.Id,
            RulesetId = game.RulesetId,
            ScenarioId = game.ScenarioId,
            PlayerId = game.PlayerId,
            Messages = [new MessageDto(message.Text)]
        };
    }

    public async Task<GameDto?> GetGameAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        Game? game = await context.Games
            .AsNoTracking()
            .Include(g => g.Messages)
            .FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken);

        if (game == null)
        {
            return null;
        }

        return new GameDto
        {
            GameId = game.Id,
            RulesetId = game.RulesetId,
            ScenarioId = game.ScenarioId,
            PlayerId = game.PlayerId,
            Messages = game.Messages
                .OrderBy(m => m.CreatedAt)
                .Select(m => new MessageDto(m.Text))
                .ToArray()
        };
    }

    public async Task<GameDto[]> GetGamesAsync(CancellationToken cancellationToken = default)
    {
        Game[] games = await context.Games
            .AsNoTracking()
            .ToArrayAsync(cancellationToken: cancellationToken);

        return games.Select(g => new GameDto()
        {
            GameId = g.Id,
            PlayerId = g.PlayerId,
            RulesetId = g.RulesetId,
            ScenarioId = g.ScenarioId,
            Messages = null
        }).ToArray();
    }
}
