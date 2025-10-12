using FastEndpoints;
using MattEland.Jaimes.ApiService.Requests;
using MattEland.Jaimes.ApiService.Responses;
using MattEland.Jaimes.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class NewGameEndpoint : Endpoint<NewGameRequest, NewGameResponse>
{
    public required IGameService GameService { get; set; }

    public override void Configure()
    {
        Post("/games/");
        AllowAnonymous();
        Description(b => b
            .Produces<NewGameResponse>(201)
            .Produces(400)
            .WithTags("Games"));
    }

    public override async Task HandleAsync(NewGameRequest req, CancellationToken ct)
    {
        var gameDto = await GameService.CreateGameAsync(req.RulesetId, req.ScenarioId, req.PlayerId, ct);

        NewGameResponse game = new()
        {
            GameId = gameDto.GameId,
            Messages = gameDto.Messages.Select(m => new MessageResponse(m.Text)).ToArray()
        };
        await Send.CreatedAtAsync<GameStateEndpoint>(game, responseBody: game, verb: Http.GET, cancellation: ct);
    }
}