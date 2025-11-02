using FastEndpoints;
using MattEland.Jaimes.ApiService.Responses;
using MattEland.Jaimes.ServiceDefinitions;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class ListPlayersEndpoint : Ep.NoReq.Res<PlayerListResponse>
{
    public required IPlayersService PlayersService { get; set; }

    public override void Configure()
    {
        Get("/players");
        AllowAnonymous();
        Description(b => b
        .Produces<PlayerListResponse>()
        .WithTags("Players"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var players = await PlayersService.GetPlayersAsync(ct);
        await Send.OkAsync(new PlayerListResponse
        {
            Players = players.Select(p => new PlayerInfoResponse
            {
                Id = p.Id,
                RulesetId = p.RulesetId,
                Description = p.Description,
                Name = p.Name
            }).ToArray()
        }, cancellation: ct);
    }
}
