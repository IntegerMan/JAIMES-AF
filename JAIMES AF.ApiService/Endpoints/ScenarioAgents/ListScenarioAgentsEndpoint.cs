using MattEland.Jaimes.Domain;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints.ScenarioAgents;

public class ListScenarioAgentsEndpoint : Ep.NoReq.Res<ScenarioAgentListResponse>
{
    public required IScenarioAgentsService ScenarioAgentsService { get; set; }

    public override void Configure()
    {
        Get("/scenarios/{scenarioId}/agents");
        AllowAnonymous();
        Description(b => b
            .Produces<ScenarioAgentListResponse>()
            .WithTags("Scenario Agents"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        string? scenarioId = Route<string>("scenarioId", true);
        if (string.IsNullOrEmpty(scenarioId))
        {
            ThrowError("Scenario ID is required");
            return;
        }

        ScenarioAgentDto[] scenarioAgents = await ScenarioAgentsService.GetScenarioAgentsAsync(scenarioId, ct);
        await Send.OkAsync(new ScenarioAgentListResponse
        {
            ScenarioAgents = scenarioAgents.Select(sa => new ScenarioAgentResponse
            {
                ScenarioId = sa.ScenarioId,
                AgentId = sa.AgentId,
                InstructionVersionId = sa.InstructionVersionId
            }).ToArray()
        }, ct);
    }
}



