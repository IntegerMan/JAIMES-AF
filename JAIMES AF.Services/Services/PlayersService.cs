using MattEland.Jaimes.Domain;
using Microsoft.EntityFrameworkCore;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.ServiceDefinitions;
using MattEland.Jaimes.ServiceLayer.Mapping;

namespace MattEland.Jaimes.ServiceLayer.Services;

public class PlayersService(JaimesDbContext context) : IPlayersService
{
    public async Task<PlayerDto[]> GetPlayersAsync(CancellationToken cancellationToken = default)
    {
        var players = await context.Players
        .AsNoTracking()
        .ToArrayAsync(cancellationToken);

        return players.ToDto();
    }
}
