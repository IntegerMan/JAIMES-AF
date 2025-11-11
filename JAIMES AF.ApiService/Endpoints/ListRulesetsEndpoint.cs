using FastEndpoints;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class ListRulesetsEndpoint : Ep.NoReq.Res<RulesetListResponse>
{
    public required IRulesetsService RulesetsService { get; set; }

    public override void Configure()
    {
        Get("/rulesets");
        AllowAnonymous();
        Description(b => b
        .Produces<RulesetListResponse>()
        .WithTags("Rulesets"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var rulesets = await RulesetsService.GetRulesetsAsync(ct);

        await Send.OkAsync(new RulesetListResponse
        {
            Rulesets = rulesets.Select(r => new RulesetInfoResponse
            {
                Id = r.Id,
                Name = r.Name
            }).ToArray()
        }, cancellation: ct);
    }
}
