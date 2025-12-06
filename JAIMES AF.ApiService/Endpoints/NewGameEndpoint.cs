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
        try
        {
            GameDto gameDto = await GameService.CreateGameAsync(req.ScenarioId, req.PlayerId, ct);

            NewGameResponse game = new()
            {
                GameId = gameDto.GameId,
                Messages = (gameDto.Messages ?? []).Select(m => m.ToResponse()).ToArray()
            };
            await Send.CreatedAtAsync<GameStateEndpoint>(game, game, Http.GET, cancellation: ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message);
        }
    }
}