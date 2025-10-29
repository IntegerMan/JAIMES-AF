using MattEland.Jaimes.Services.Models;

namespace MattEland.Jaimes.ServiceLayer.Services;

public interface IPlayersService
{
 Task<PlayerDto[]> GetPlayersAsync(CancellationToken cancellationToken = default);
}
