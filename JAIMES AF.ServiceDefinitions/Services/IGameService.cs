using MattEland.Jaimes.Domain;
using MattEland.Jaimes.Repositories.Entities;

namespace MattEland.Jaimes.ServiceDefinitions.Services;

public interface IGameService
{
    Task<GameDto> CreateGameAsync(string scenarioId, string playerId, CancellationToken cancellationToken = default);
    Task<GameDto?> GetGameAsync(Guid gameId, CancellationToken cancellationToken = default);
    Task<GameDto[]> GetGamesAsync(CancellationToken cancellationToken = default);
    Task AddMessagesAsync(IEnumerable<Message> messages, CancellationToken cancellationToken = default);
}
