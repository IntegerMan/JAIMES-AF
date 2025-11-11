using MattEland.Jaimes.Domain;

namespace MattEland.Jaimes.ServiceDefinitions.Services;

public interface IPlayersService
{
 Task<PlayerDto[]> GetPlayersAsync(CancellationToken cancellationToken = default);
}
