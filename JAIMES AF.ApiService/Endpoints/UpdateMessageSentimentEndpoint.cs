using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ApiService.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace MattEland.Jaimes.ApiService.Endpoints;

public static class UpdateMessageSentimentEndpoint
{
    public static void MapUpdateMessageSentiment(this IEndpointRouteBuilder app)
    {
        app.MapPut("/messages/{id}/sentiment", async (
                [FromRoute] int id,
                UpdateMessageSentimentRequest request,
                [FromServices] JaimesDbContext context,
                [FromServices] IHubContext<MessageHub, IMessageHubClient> hubContext,
                CancellationToken cancellationToken) =>
            {
                if (request.Sentiment < -1 || request.Sentiment > 1)
                {
                    return Results.BadRequest("Sentiment must be between -1 and 1.");
                }

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

                // Notify clients via SignalR directly
                string groupName = MessageHub.GetGameGroupName(message.GameId);
                MessageUpdateNotification notification = new()
                {
                    MessageId = message.Id,
                    GameId = message.GameId,
                    UpdateType = MessageUpdateType.SentimentAnalyzed,
                    Sentiment = request.Sentiment,
                    SentimentConfidence = 1.0,
                    SentimentSource = (int)SentimentSource.Player
                };

                await hubContext.Clients.Group(groupName).MessageUpdated(notification);

                return Results.Ok();
            })
            .WithName("UpdateMessageSentiment")
            .WithSummary("Updates the sentiment of a message")
            .WithDescription(
                "Manually updates the sentiment of a message, setting the source to Player and confidence to 100%.");
    }
}
