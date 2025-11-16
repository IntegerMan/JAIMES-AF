using FastEndpoints;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.Domain;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class UpdateScenarioEndpoint : Endpoint<UpdateScenarioRequest, ScenarioResponse>
{
    public required IScenariosService ScenariosService { get; set; }

    public override void Configure()
    {
        Put("/scenarios/{id}");
        AllowAnonymous();
        Description(b => b
            .Produces<ScenarioResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Scenarios"));
    }

    public override async Task HandleAsync(UpdateScenarioRequest req, CancellationToken ct)
    {
        string? id = Route<string>("id", isRequired: true);
        if (string.IsNullOrEmpty(id))
        {
            ThrowError("Scenario ID is required");
            return;
        }

        try
        {
            ScenarioDto scenario = await ScenariosService.UpdateScenarioAsync(
                id,
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

            await Send.OkAsync(response, cancellation: ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message);
        }
    }
}

