using MattEland.Jaimes.Domain;

namespace MattEland.Jaimes.ServiceDefinitions;

public interface IPlayersService
{
 Task<PlayerDto[]> GetPlayersAsync(CancellationToken cancellationToken = default);
}
