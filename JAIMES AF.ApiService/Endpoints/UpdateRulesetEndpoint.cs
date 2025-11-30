using FastEndpoints;
using MattEland.Jaimes.Domain;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class UpdateRulesetEndpoint : Endpoint<UpdateRulesetRequest, RulesetResponse>
{
    public required IRulesetsService RulesetsService { get; set; }

    public override void Configure()
    {
        Put("/rulesets/{id}");
        AllowAnonymous();
        Description(b => b
            .Produces<RulesetResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Rulesets"));
    }

    public override async Task HandleAsync(UpdateRulesetRequest req, CancellationToken ct)
    {
        string? id = Route<string>("id", isRequired: true);
        if (string.IsNullOrEmpty(id))
        {
            ThrowError("Ruleset ID is required");
            return;
        }

        try
        {
            RulesetDto ruleset = await RulesetsService.UpdateRulesetAsync(
                id,
                req.Name,
                ct);

            RulesetResponse response = new()
            {
                Id = ruleset.Id,
                Name = ruleset.Name
            };

            await Send.OkAsync(response, cancellation: ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message);
        }
    }
}

