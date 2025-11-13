using MattEland.Jaimes.Domain;
using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.ServiceDefinitions.Services;

public interface IChatService
{
    Task<(string[] Messages, string ThreadJson)> GetChatResponseAsync(GameDto game, string message, CancellationToken cancellationToken = default);
    Task<ChatResponse> ProcessChatMessageAsync(Guid gameId, string message, CancellationToken cancellationToken = default);
}