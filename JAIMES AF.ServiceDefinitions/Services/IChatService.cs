using MattEland.Jaimes.Domain;

namespace MattEland.Jaimes.ServiceDefinitions.Services;

public interface IChatService
{
    Task<(string[] Messages, string ThreadJson)> GetChatResponseAsync(GameDto game, string message, CancellationToken cancellationToken = default);
}