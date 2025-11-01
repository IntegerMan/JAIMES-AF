using MattEland.Jaimes.Domain;

namespace MattEland.Jaimes.ServiceDefinitions;

public interface IChatService
{
    Task<string[]> GetChatResponseAsync(GameDto game, string message, CancellationToken cancellationToken = default);
}