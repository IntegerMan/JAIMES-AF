using FastEndpoints;
using MattEland.Jaimes.ApiService.Responses;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class GameStateEndpoint : EndpointWithoutRequest<GameStateResponse>
{
    public override void Configure()
    {
        Get("/games/{gameId:guid}");
        AllowAnonymous();
        Description(b => b
            .Produces<GameStateResponse>(200)
            .Produces(400)
            .WithTags("Games"));
    }
    public override async Task HandleAsync(CancellationToken ct)
    {
        GameStateResponse gameState = new()
        {
            GameId = Query<Guid>("gameId", isRequired: true),
            Messages = [
                new MessageResponse("Game state is not implemented yet.")
            ]
        };
        await Send.OkAsync(gameState, cancellation: ct);
    }
}
