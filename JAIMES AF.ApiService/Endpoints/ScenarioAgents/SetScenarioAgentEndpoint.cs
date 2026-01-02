using MattEland.Jaimes.Domain;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints.ScenarioAgents;

public class SetScenarioAgentEndpoint : Endpoint<SetScenarioAgentRequest, ScenarioAgentResponse>
{
    public required IScenarioAgentsService ScenarioAgentsService { get; set; }

    public override void Configure()
    {
        Put("/scenarios/{scenarioId}/agents");
        AllowAnonymous();
        Description(b => b
            .Produces<ScenarioAgentResponse>(StatusCodes.Status200OK)
            .WithTags("Scenario Agents"));
    }

    public override async Task HandleAsync(SetScenarioAgentRequest req, CancellationToken ct)
    {
        string? scenarioId = Route<string>("scenarioId", true);
        if (string.IsNullOrEmpty(scenarioId))
        {
            ThrowError("Scenario ID is required");
            return;
        }

        try
        {
            ScenarioAgentDto scenarioAgent = await ScenarioAgentsService.SetScenarioAgentAsync(
                scenarioId, req.AgentId, req.InstructionVersionId, ct);

            ScenarioAgentResponse response = new()
            {
                ScenarioId = scenarioAgent.ScenarioId,
                AgentId = scenarioAgent.AgentId,
                InstructionVersionId = scenarioAgent.InstructionVersionId
            };

            await Send.OkAsync(response, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message);
        }
    }
}



