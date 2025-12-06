namespace MattEland.Jaimes.ApiService.Endpoints;

public class GetRulesetEndpoint : EndpointWithoutRequest<RulesetResponse>
{
    public required IRulesetsService RulesetsService { get; set; }

    public override void Configure()
    {
        Get("/rulesets/{id}");
        AllowAnonymous();
        Description(b => b
            .Produces<RulesetResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Rulesets"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        string? id = Route<string>("id", true);
        if (string.IsNullOrEmpty(id))
        {
            ThrowError("Ruleset ID is required");
            return;
        }

        try
        {
            RulesetDto ruleset = await RulesetsService.GetRulesetAsync(id, ct);

            RulesetResponse response = new()
            {
                Id = ruleset.Id,
                Name = ruleset.Name
            };

            await Send.OkAsync(response, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message);
        }
    }
}