using FastEndpoints;
using MattEland.Jaimes.ApiService.Responses;
using MattEland.Jaimes.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class GameStateEndpoint : EndpointWithoutRequest<GameStateResponse>
{
    public required IGameService GameService { get; set; }

    public override void Configure()
    {
        Get("/games/{gameId:guid}");
        AllowAnonymous();
        Description(b => b
            .Produces<GameStateResponse>(200)
            .Produces(404)
            .WithTags("Games"));
    }
    public override async Task HandleAsync(CancellationToken ct)
    {
        Guid gameId = Query<Guid>("gameId", isRequired: true);
        var gameDto = await GameService.GetGameAsync(gameId, ct);

        if (gameDto == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        GameStateResponse gameState = new()
        {
            GameId = gameDto.GameId,
            Messages = gameDto.Messages.Select(m => new MessageResponse(m.Text)).ToArray()
        };
        await Send.OkAsync(gameState, cancellation: ct);
    }
}
