using FastEndpoints;
using MattEland.Jaimes.Domain;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class CreateScenarioEndpoint : Endpoint<CreateScenarioRequest, ScenarioResponse>
{
    public required IScenariosService ScenariosService { get; set; }

    public override void Configure()
    {
        Post("/scenarios");
        AllowAnonymous();
        Description(b => b
            .Produces<ScenarioResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .WithTags("Scenarios"));
    }

    public override async Task HandleAsync(CreateScenarioRequest req, CancellationToken ct)
    {
        try
        {
            ScenarioDto scenario = await ScenariosService.CreateScenarioAsync(
                req.Id,
                req.RulesetId,
                req.Description,
                req.Name,
                req.SystemPrompt,
                req.NewGameInstructions,
                ct);

            ScenarioResponse response = new()
            {
                Id = scenario.Id,
                RulesetId = scenario.RulesetId,
                Description = scenario.Description,
                Name = scenario.Name,
                SystemPrompt = scenario.SystemPrompt,
                NewGameInstructions = scenario.NewGameInstructions
            };

            await Send.CreatedAtAsync<GetScenarioEndpoint>(response, response, verb: Http.GET, cancellation: ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message);
        }
    }
}

