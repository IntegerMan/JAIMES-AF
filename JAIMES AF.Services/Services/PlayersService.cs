using MattEland.Jaimes.Domain;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.ServiceLayer.Mapping;
using Microsoft.EntityFrameworkCore;

namespace MattEland.Jaimes.ServiceLayer.Services;

public class PlayersService(JaimesDbContext context) : IPlayersService
{
    public async Task<PlayerDto[]> GetPlayersAsync(CancellationToken cancellationToken = default)
    {
        Player[] players = await context.Players
        .AsNoTracking()
        .ToArrayAsync(cancellationToken);

        return players.ToDto();
    }

    public async Task<PlayerDto> GetPlayerAsync(string id, CancellationToken cancellationToken = default)
    {
        Player? player = await context.Players
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (player == null)
        {
            throw new ArgumentException($"Player with id '{id}' not found.", nameof(id));
        }

        return player.ToDto();
    }

    public async Task<PlayerDto> CreatePlayerAsync(string id, string rulesetId, string? description, string name, CancellationToken cancellationToken = default)
    {
        // Check if player already exists
        bool exists = await context.Players
            .AnyAsync(p => p.Id == id, cancellationToken);

        if (exists)
        {
            throw new ArgumentException($"Player with id '{id}' already exists.", nameof(id));
        }

        // Verify ruleset exists
        bool rulesetExists = await context.Rulesets
            .AnyAsync(r => r.Id == rulesetId, cancellationToken);

        if (!rulesetExists)
        {
            throw new ArgumentException($"Ruleset with id '{rulesetId}' not found.", nameof(rulesetId));
        }

        Player newPlayer = new()
        {
            Id = id,
            RulesetId = rulesetId,
            Description = description,
            Name = name
        };

        context.Players.Add(newPlayer);
        await context.SaveChangesAsync(cancellationToken);

        return newPlayer.ToDto();
    }

    public async Task<PlayerDto> UpdatePlayerAsync(string id, string rulesetId, string? description, string name, CancellationToken cancellationToken = default)
    {
        Player? player = await context.Players
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (player == null)
        {
            throw new ArgumentException($"Player with id '{id}' not found.", nameof(id));
        }

        // Verify ruleset exists
        bool rulesetExists = await context.Rulesets
            .AnyAsync(r => r.Id == rulesetId, cancellationToken);

        if (!rulesetExists)
        {
            throw new ArgumentException($"Ruleset with id '{rulesetId}' not found.", nameof(rulesetId));
        }

        player.RulesetId = rulesetId;
        player.Description = description;
        player.Name = name;

        await context.SaveChangesAsync(cancellationToken);

        return player.ToDto();
    }
}
