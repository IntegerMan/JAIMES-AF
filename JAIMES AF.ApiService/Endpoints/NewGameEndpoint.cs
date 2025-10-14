using FastEndpoints;
using MattEland.Jaimes.ApiService.Requests;
using MattEland.Jaimes.ApiService.Responses;
using MattEland.Jaimes.ServiceLayer.Services;
using MattEland.Jaimes.Services.Models;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class NewGameEndpoint : Endpoint<NewGameRequest, NewGameResponse>
{
    public required IGameService GameService { get; set; }

    public override void Configure()
    {
        Post("/games/");
        AllowAnonymous();
        Description(b => b
            .Produces<NewGameResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .WithTags("Games"));
    }

    public override async Task HandleAsync(NewGameRequest req, CancellationToken ct)
    {
        GameDto gameDto = await GameService.CreateGameAsync(req.RulesetId, req.ScenarioId, req.PlayerId, ct);

        NewGameResponse game = new()
        {
            GameId = gameDto.GameId,
            Messages = gameDto.Messages.Select(m => new MessageResponse(m.Text)).ToArray()
        };
        await Send.CreatedAtAsync<GameStateEndpoint>(game, responseBody: game, verb: Http.GET, cancellation: ct);
    }
}