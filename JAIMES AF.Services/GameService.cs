using Microsoft.EntityFrameworkCore;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.Services.Models;

namespace MattEland.Jaimes.Services;

public class GameService : IGameService
{
    private readonly JaimesDbContext _context;

    public GameService(JaimesDbContext context)
    {
        _context = context;
    }

    public async Task<GameDto> CreateGameAsync(string rulesetId, string scenarioId, string playerId, CancellationToken cancellationToken = default)
    {
        var game = new Game
        {
            Id = Guid.NewGuid(),
            RulesetId = rulesetId,
            ScenarioId = scenarioId,
            PlayerId = playerId,
            CreatedAt = DateTime.UtcNow
        };

        var message = new Message
        {
            GameId = game.Id,
            Text = "Hello World",
            CreatedAt = DateTime.UtcNow
        };

        _context.Games.Add(game);
        _context.Messages.Add(message);
        await _context.SaveChangesAsync(cancellationToken);

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
        var game = await _context.Games
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
}
