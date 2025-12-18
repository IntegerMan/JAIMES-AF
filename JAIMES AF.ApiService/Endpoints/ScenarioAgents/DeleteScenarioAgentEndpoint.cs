using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints.ScenarioAgents;

public class DeleteScenarioAgentEndpoint : EndpointWithoutRequest
{
    public required IScenarioAgentsService ScenarioAgentsService { get; set; }

    public override void Configure()
    {
        Delete("/scenarios/{scenarioId}/agents/{agentId}");
        AllowAnonymous();
        Description(b => b
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Scenario Agents"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        string? scenarioId = Route<string>("scenarioId", true);
        string? agentId = Route<string>("agentId", true);
        
        if (string.IsNullOrEmpty(scenarioId) || string.IsNullOrEmpty(agentId))
        {
            ThrowError("Scenario ID and Agent ID are required");
            return;
        }

        try
        {
            await ScenarioAgentsService.RemoveScenarioAgentAsync(scenarioId, agentId, ct);
            await Send.NoContentAsync(ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message);
        }
    }
}
