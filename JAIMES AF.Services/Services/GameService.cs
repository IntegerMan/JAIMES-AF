using MattEland.Jaimes.Domain;
using Microsoft.EntityFrameworkCore;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ServiceLayer.Services;

public class GameService(JaimesDbContext context) : IGameService
{
    public async Task<GameDto> CreateGameAsync(string scenarioId, string playerId, CancellationToken cancellationToken = default)
    {
        // Validate that the player exists
        var player = await context.Players.FindAsync([playerId], cancellationToken);
        if (player == null)
        {
            throw new ArgumentException($"Player '{playerId}' does not exist.", nameof(playerId));
        }

        // Validate that the scenario exists
        var scenario = await context.Scenarios.FindAsync([scenarioId], cancellationToken);
        if (scenario == null)
        {
            throw new ArgumentException($"Scenario '{scenarioId}' does not exist.", nameof(scenarioId));
        }

        // Validate that player and scenario have the same ruleset
        if (player.RulesetId != scenario.RulesetId)
        {
            throw new ArgumentException($"Player '{playerId}' uses ruleset '{player.RulesetId}' but scenario '{scenarioId}' uses ruleset '{scenario.RulesetId}'. They must use the same ruleset.", nameof(scenarioId));
        }

        // Use the ruleset from the player (which we've validated matches the scenario)
        string rulesetId = player.RulesetId;

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
            Player = player,
            PlayerId = playerId,
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
            Messages = [new MessageDto(message.Text, playerId, player.Name, message.CreatedAt)],
            ScenarioName = scenario.Name,
            RulesetName = (await context.Rulesets.FindAsync([rulesetId], cancellationToken))?.Name ?? rulesetId,
            PlayerName = player.Name
        };
    }

    public async Task<GameDto?> GetGameAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        Game? game = await context.Games
            .AsNoTracking()
            .Include(g => g.Messages).ThenInclude(message => message.Player)
            .Include(g => g.Scenario)
            .Include(g => g.Player)
            .Include(g => g.Ruleset)
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
                .Select(m => new MessageDto(m.Text, m.PlayerId, m.Player?.Name ?? "Game Master", m.CreatedAt))
                .ToArray(),
            ScenarioName = game.Scenario?.Name ?? game.ScenarioId,
            RulesetName = game.Ruleset?.Name ?? game.RulesetId,
            PlayerName = game.Player?.Name ?? game.PlayerId
        };
    }

    public async Task<GameDto[]> GetGamesAsync(CancellationToken cancellationToken = default)
    {
        Game[] games = await context.Games
            .AsNoTracking()
            .Include(g => g.Scenario)
            .Include(g => g.Player)
            .Include(g => g.Ruleset)
            .ToArrayAsync(cancellationToken: cancellationToken);

        return games.Select(g => new GameDto()
        {
            GameId = g.Id,
            PlayerId = g.PlayerId,
            RulesetId = g.RulesetId,
            ScenarioId = g.ScenarioId,
            Messages = null,
            ScenarioName = g.Scenario?.Name ?? g.ScenarioId,
            RulesetName = g.Ruleset?.Name ?? g.RulesetId,
            PlayerName = g.Player?.Name ?? g.PlayerId
        }).ToArray();
    }
}
