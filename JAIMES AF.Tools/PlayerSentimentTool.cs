using MattEland.Jaimes.Domain;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;

namespace MattEland.Jaimes.Tools;

/// <summary>
/// Tool that provides recent sentiment analysis results for the player in the current game.
/// </summary>
public class PlayerSentimentTool(GameDto game, IServiceProvider serviceProvider)
{
    private readonly GameDto _game = game ?? throw new ArgumentNullException(nameof(game));

    private readonly IServiceProvider _serviceProvider =
        serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    /// <summary>
    /// Retrieves the last 5 most recent sentiment analysis results for the player in the current game.
    /// This helps understand the player's frustration level and emotional state.
    /// </summary>
    /// <returns>A formatted string containing the most recent sentiment analysis results with message previews.</returns>
    [Description(
        "Retrieves the last 5 most recent sentiment analysis results for the player in the current game. This helps understand the player's frustration level and emotional state. Use this tool when you need to gauge how the player is feeling about the game or recent interactions.")]
    public async Task<string> GetRecentSentimentsAsync()
    {
        Guid gameId = _game.GameId;
        string playerId = _game.Player.Id;

        // Create a scope to resolve IDbContextFactory on each call
        // This ensures we get a fresh scoped instance and avoid ObjectDisposedException
        // when the tool outlives the scope that created it
        using IServiceScope scope = _serviceProvider.CreateScope();
        IDbContextFactory<JaimesDbContext>? contextFactory =
            scope.ServiceProvider.GetService<IDbContextFactory<JaimesDbContext>>();
        if (contextFactory == null)
        {
            return "Database context factory is not available.";
        }

        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync();

        // Query messages with sentiment for the current game and player
        Message[] messagesWithSentiment = await context.Messages
            .Where(m => m.GameId == gameId
                        && m.PlayerId == playerId
                        && m.Sentiment != null)
            .OrderByDescending(m => m.CreatedAt)
            .Take(5)
            .ToArrayAsync();

        if (messagesWithSentiment.Length == 0)
        {
            return "No sentiment analysis results available for this player in the current game.";
        }

        // Format results
        List<string> resultTexts = new();
        foreach (Message message in messagesWithSentiment)
        {
            string sentimentLabel = message.Sentiment switch
            {
                1 => "Positive",
                -1 => "Negative",
                0 => "Neutral",
                _ => "Unknown"
            };

            string messagePreview = message.Text.Length > 100
                ? message.Text.Substring(0, 100) + "..."
                : message.Text;

            resultTexts.Add(
                $"[{message.CreatedAt:yyyy-MM-dd HH:mm:ss}] {sentimentLabel} ({message.Sentiment}): {messagePreview}");
        }

        return string.Join("\n", resultTexts);
    }
}

