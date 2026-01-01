namespace MattEland.Jaimes.ServiceDefinitions.Services;

public interface IGameService
{
    Task<GameDto> CreateGameAsync(string scenarioId,
        string playerId,
        string? title,
        CancellationToken cancellationToken = default);

    Task<GameDto?> GetGameAsync(Guid gameId, CancellationToken cancellationToken = default);
    Task<GameDto[]> GetGamesAsync(CancellationToken cancellationToken = default);

    Task<GameDto?> UpdateGameAsync(Guid gameId, string? title, string? agentId = null, int? instructionVersionId = null,
        CancellationToken cancellationToken = default);

    Task DeleteGameAsync(Guid gameId, CancellationToken cancellationToken = default);
}