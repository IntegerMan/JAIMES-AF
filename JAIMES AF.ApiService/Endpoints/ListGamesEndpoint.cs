using FastEndpoints;
using MattEland.Jaimes.ServiceDefinitions;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.Domain;
using MattEland.Jaimes.ServiceDefinitions;

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
                ScenarioId = g.ScenarioId,
                ScenarioName = g.ScenarioName,
                RulesetId = g.RulesetId,
                RulesetName = g.RulesetName,
                PlayerId = g.PlayerId,
                PlayerName = g.PlayerName
            }).ToArray()
        }, cancellation: ct);
    }
}
