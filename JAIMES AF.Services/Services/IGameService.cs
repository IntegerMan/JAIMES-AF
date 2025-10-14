using MattEland.Jaimes.Services.Models;

namespace MattEland.Jaimes.ServiceLayer.Services;

public interface IGameService
{
    Task<GameDto> CreateGameAsync(string scenarioId, string playerId, CancellationToken cancellationToken = default);
    Task<GameDto?> GetGameAsync(Guid gameId, CancellationToken cancellationToken = default);
    Task<GameDto[]> GetGamesAsync(CancellationToken cancellationToken = default);
}
