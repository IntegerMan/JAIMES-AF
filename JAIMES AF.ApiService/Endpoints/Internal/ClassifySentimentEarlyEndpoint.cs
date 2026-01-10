using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints.Internal;

/// <summary>
/// Internal endpoint for early sentiment classification before message persistence.
/// Enqueues a sentiment classification task and returns a tracking GUID for client correlation.
/// </summary>
public static class ClassifySentimentEarlyEndpoint
{
    public static void MapClassifySentimentEarlyEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/internal/classify-sentiment-early",
                async (ClassifySentimentEarlyRequest request,
                    IMessagePublisher messagePublisher,
                    ILogger<IMessagePublisher> logger,
                    CancellationToken cancellationToken) =>
                {
                    // Generate tracking GUID for correlation
                    Guid trackingGuid = Guid.NewGuid();

                    // Create queue message for worker
                    EarlySentimentClassificationMessage queueMessage = new()
                    {
                        TrackingGuid = trackingGuid,
                        GameId = request.GameId,
                        MessageText = request.MessageText
                    };

                    // Publish to queue
                    await messagePublisher.PublishAsync(queueMessage, cancellationToken);

                    logger.LogDebug(
                        "Enqueued early sentiment classification for game {GameId} with tracking GUID {TrackingGuid}",
                        request.GameId,
                        trackingGuid);

                    // Return tracking GUID to client
                    return Results.Ok(new ClassifySentimentEarlyResponse
                    {
                        TrackingGuid = trackingGuid
                    });
                })
            .WithName("ClassifySentimentEarly")
            .WithTags("Internal")
            .Produces<ClassifySentimentEarlyResponse>(200);
    }
}
