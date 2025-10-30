using Microsoft.EntityFrameworkCore;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Services.Models;

namespace MattEland.Jaimes.ServiceLayer.Services;

public class PlayersService(JaimesDbContext context) : IPlayersService
{
    public async Task<PlayerDto[]> GetPlayersAsync(CancellationToken cancellationToken = default)
    {
        var players = await context.Players
        .AsNoTracking()
        .ToArrayAsync(cancellationToken);

        return players.Select(p => new PlayerDto
        {
            Id = p.Id,
            RulesetId = p.RulesetId,
            Description = p.Description
        }).ToArray();
    }
}
