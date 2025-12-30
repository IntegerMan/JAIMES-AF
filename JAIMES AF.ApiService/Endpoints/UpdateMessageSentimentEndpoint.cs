using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MattEland.Jaimes.ApiService.Endpoints;

public static class UpdateMessageSentimentEndpoint
{
    public static void MapUpdateMessageSentiment(this IEndpointRouteBuilder app)
    {
        app.MapPut("/messages/{id}/sentiment", async (
                [FromRoute] int id,
                [Microsoft.AspNetCore.Mvc.FromBody] UpdateMessageSentimentRequest request,
                [FromServices] JaimesDbContext context,
                [FromServices] IMessageUpdateNotifier notifier,
                CancellationToken cancellationToken) =>
            {
                Message? message = await context.Messages
                    .Include(m => m.MessageSentiment)
                    .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

                if (message == null)
                {
                    return Results.NotFound();
                }

                DateTime now = DateTime.UtcNow;

                if (message.MessageSentiment == null)
                {
                    message.MessageSentiment = new MessageSentiment
                    {
                        MessageId = message.Id,
                        Sentiment = request.Sentiment,
                        Confidence = 1.0,
                        SentimentSource = SentimentSource.Player,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    context.MessageSentiments.Add(message.MessageSentiment);
                }
                else
                {
                    message.MessageSentiment.Sentiment = request.Sentiment;
                    message.MessageSentiment.Confidence = 1.0;
                    message.MessageSentiment.SentimentSource = SentimentSource.Player;
                    message.MessageSentiment.UpdatedAt = now;
                }

                await context.SaveChangesAsync(cancellationToken);

                // Notify clients about the update
                await notifier.NotifySentimentAnalyzedAsync(
                    message.Id,
                    message.GameId,
                    request.Sentiment,
                    1.0,
                    cancellationToken);

                return Results.Ok();
            })
            .WithName("UpdateMessageSentiment")
            .WithSummary("Updates the sentiment of a message")
            .WithDescription(
                "Manually updates the sentiment of a message, setting the source to Player and confidence to 100%.");
    }
}
