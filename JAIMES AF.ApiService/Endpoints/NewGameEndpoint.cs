using FastEndpoints;
using MattEland.Jaimes.ApiService.Requests;
using MattEland.Jaimes.ApiService.Responses;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class NewGameEndpoint : Endpoint<NewGameRequest, NewGameResponse>
{
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
        NewGameResponse game = new()
        {
            GameId = Guid.NewGuid(),
            Messages =
            [
                new MessageResponse("Hello World")
            ]
        };
        await Send.CreatedAtAsync<GameStateEndpoint>(game, responseBody: game, verb: Http.GET, cancellation: ct);
    }
}