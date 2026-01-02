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
                Games = games
                    .OrderByDescending(g => g.LastPlayedAt ?? g.CreatedAt)
                    .Select(g => new GameInfoResponse
                    {
                        GameId = g.GameId,
                        Title = g.Title,
                        ScenarioId = g.Scenario.Id,
                        ScenarioName = g.Scenario.Name,
                        RulesetId = g.Ruleset.Id,
                        RulesetName = g.Ruleset.Name,
                        PlayerId = g.Player.Id,
                        PlayerName = g.Player.Name,
                        CreatedAt = g.CreatedAt,
                        LastPlayedAt = g.LastPlayedAt,
                        AgentId = g.AgentId,
                        AgentName = g.AgentName,
                        InstructionVersionId = g.InstructionVersionId,
                        VersionNumber = g.VersionNumber
                    })
                    .ToArray()
            },
            ct);
    }
}