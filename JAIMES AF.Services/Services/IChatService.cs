using MattEland.Jaimes.Services.Models;

namespace MattEland.Jaimes.ServiceLayer.Services;

public interface IChatService
{
    Task<string[]> GetChatResponseAsync(GameDto game, string message, CancellationToken cancellationToken = default);
}