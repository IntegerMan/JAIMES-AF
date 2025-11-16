using FastEndpoints;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.Domain;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class CreateRulesetEndpoint : Endpoint<CreateRulesetRequest, RulesetResponse>
{
    public required IRulesetsService RulesetsService { get; set; }

    public override void Configure()
    {
        Post("/rulesets");
        AllowAnonymous();
        Description(b => b
            .Produces<RulesetResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .WithTags("Rulesets"));
    }

    public override async Task HandleAsync(CreateRulesetRequest req, CancellationToken ct)
    {
        try
        {
            RulesetDto ruleset = await RulesetsService.CreateRulesetAsync(
                req.Id,
                req.Name,
                ct);

            RulesetResponse response = new()
            {
                Id = ruleset.Id,
                Name = ruleset.Name
            };

            await Send.CreatedAtAsync<GetRulesetEndpoint>(response, response, verb: Http.GET, cancellation: ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message);
        }
    }
}

