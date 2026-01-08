namespace MattEland.Jaimes.ApiService.Endpoints;

public class ListRulesetsEndpoint : Ep.NoReq.Res<RulesetListResponse>
{
    public required IRulesetsService RulesetsService { get; set; }
    public required IDbContextFactory<JaimesDbContext> DbContextFactory { get; set; }

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
        RulesetDto[] rulesets = await RulesetsService.GetRulesetsAsync(ct);

        // Get sourcebook counts per ruleset
        await using JaimesDbContext context = await DbContextFactory.CreateDbContextAsync(ct);
        Dictionary<string, int> sourcebookCounts = await context.DocumentMetadata
            .Where(d => d.DocumentKind == DocumentKinds.Sourcebook)
            .GroupBy(d => d.RulesetId)
            .Select(g => new { RulesetId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RulesetId, x => x.Count, ct);

        await Send.OkAsync(new RulesetListResponse
            {
                Rulesets = rulesets.Select(r => new RulesetInfoResponse
                    {
                        Id = r.Id,
                        Name = r.Name,
                        Description = r.Description,
                        SourcebookCount = sourcebookCounts.GetValueOrDefault(r.Id, 0)
                    })
                    .ToArray()
            },
            ct);
    }
}