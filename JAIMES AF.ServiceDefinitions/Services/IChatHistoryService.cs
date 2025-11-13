namespace MattEland.Jaimes.ServiceDefinitions.Services;

public interface IChatHistoryService
{
    Task<string?> GetMostRecentThreadJsonAsync(Guid gameId, CancellationToken cancellationToken = default);
    Task<Guid> SaveThreadJsonAsync(Guid gameId, string threadJson, int? messageId = null, CancellationToken cancellationToken = default);
}


