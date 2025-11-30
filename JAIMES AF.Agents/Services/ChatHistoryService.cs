using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.EntityFrameworkCore;

namespace MattEland.Jaimes.Agents.Services;

public class ChatHistoryService(JaimesDbContext context) : IChatHistoryService
{
    public async Task<string?> GetMostRecentThreadJsonAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        Game? game = await context.Games
            .Include(g => g.MostRecentHistory)
            .FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken);

        return game?.MostRecentHistory?.ThreadJson;
    }

    public async Task<Guid> SaveThreadJsonAsync(Guid gameId, string threadJson, int? messageId = null, CancellationToken cancellationToken = default)
    {
        Game? game = await context.Games
            .Include(g => g.MostRecentHistory)
            .FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken);

        if (game == null)
        {
            throw new ArgumentException($"Game '{gameId}' does not exist.", nameof(gameId));
        }

        ChatHistory newHistory = new()
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            ThreadJson = threadJson,
            CreatedAt = DateTime.UtcNow,
            PreviousHistoryId = game.MostRecentHistory?.Id,
            MessageId = messageId
        };

        context.ChatHistories.Add(newHistory);
        game.MostRecentHistoryId = newHistory.Id;
        await context.SaveChangesAsync(cancellationToken);

        return newHistory.Id;
    }
}

