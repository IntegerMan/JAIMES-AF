namespace MattEland.Jaimes.ApiService.Endpoints;

public class ListScenariosEndpoint : Ep.NoReq.Res<ScenarioListResponse>
{
    public required IScenariosService ScenariosService { get; set; }

    public override void Configure()
    {
        Get("/scenarios");
        AllowAnonymous();
        Description(b => b
            .Produces<ScenarioListResponse>()
            .WithTags("Scenarios"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        ScenarioDto[] scenarios = await ScenariosService.GetScenariosAsync(ct);
        await Send.OkAsync(new ScenarioListResponse
        {
            Scenarios = scenarios.Select(s => new ScenarioInfoResponse
            {
                Id = s.Id,
                RulesetId = s.RulesetId,
                Description = s.Description,
                Name = s.Name
            })
                    .ToArray()
        },
            ct);
    }
}