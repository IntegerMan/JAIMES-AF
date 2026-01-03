namespace MattEland.Jaimes.ApiService.Endpoints;

public class LastPlayedGameEndpoint : Ep.NoReq.Res<GameInfoResponse>
{
    public required IGameService GameService { get; set; }

    public override void Configure()
    {
        Get("/games/latest");
        AllowAnonymous();
        Description(b => b
            .Produces<GameInfoResponse>()
            .Produces(404)
            .WithTags("Games"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        GameDto? game = await GameService.GetLastPlayedGameAsync(ct);

        if (game == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        GameInfoResponse response = new()
        {
            GameId = game.GameId,
            Title = game.Title,
            ScenarioId = game.Scenario.Id,
            ScenarioName = game.Scenario.Name,
            RulesetId = game.Ruleset.Id,
            RulesetName = game.Ruleset.Name,
            PlayerId = game.Player.Id,
            PlayerName = game.Player.Name,
            CreatedAt = game.CreatedAt,
            LastPlayedAt = game.LastPlayedAt
        };

        await Send.OkAsync(response, ct);
    }
}
