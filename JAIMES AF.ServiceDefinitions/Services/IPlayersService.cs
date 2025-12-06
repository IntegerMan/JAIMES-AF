namespace MattEland.Jaimes.ServiceDefinitions.Services;

public interface IPlayersService
{
    Task<PlayerDto[]> GetPlayersAsync(CancellationToken cancellationToken = default);
    Task<PlayerDto> GetPlayerAsync(string id, CancellationToken cancellationToken = default);

    Task<PlayerDto> CreatePlayerAsync(string id,
        string rulesetId,
        string? description,
        string name,
        CancellationToken cancellationToken = default);

    Task<PlayerDto> UpdatePlayerAsync(string id,
        string rulesetId,
        string? description,
        string name,
        CancellationToken cancellationToken = default);
}