using FastEndpoints;
using MattEland.Jaimes.Domain;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class ListGamesEndpoint : Ep.NoReq.Res<ListGamesResponse>
{
    public required IGameService GameService { get; set; }

    public override void Configure()
    {
        Get("/games");
        AllowAnonymous();
        Description(b => b
            .Produces<ListGamesResponse>()
            .WithTags("Games"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        GameDto[] games = await GameService.GetGamesAsync(ct);

        await Send.OkAsync(new ListGamesResponse
        {
            Games = games.Select(g => new GameInfoResponse
            {
                GameId = g.GameId,
                ScenarioId = g.Scenario.Id,
                ScenarioName = g.Scenario.Name,
                RulesetId = g.Ruleset.Id,
                RulesetName = g.Ruleset.Name,
                PlayerId = g.Player.Id,
                PlayerName = g.Player.Name
            }).ToArray()
        }, cancellation: ct);
    }
}
