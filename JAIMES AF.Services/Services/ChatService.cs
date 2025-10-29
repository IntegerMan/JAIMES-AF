using MattEland.Jaimes.Services.Models;

namespace MattEland.Jaimes.ServiceLayer.Services;

public class ChatService : IChatService
{
    public async Task<string[]> GetChatResponseAsync(GameDto game, string message, CancellationToken cancellationToken = default)
    {
        // Placeholder implementation
        await Task.Delay(100, cancellationToken); // Simulate async work

        return [$"Echo: {message}"];
    }
}
