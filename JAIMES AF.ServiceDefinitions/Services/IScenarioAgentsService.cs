using MattEland.Jaimes.Domain;

namespace MattEland.Jaimes.ServiceDefinitions.Services;

public interface IScenarioAgentsService
{
    Task<ScenarioAgentDto[]> GetScenarioAgentsAsync(string scenarioId, CancellationToken cancellationToken = default);
    Task<ScenarioAgentDto> SetScenarioAgentAsync(string scenarioId, string agentId, int instructionVersionId, CancellationToken cancellationToken = default);
    Task RemoveScenarioAgentAsync(string scenarioId, string agentId, CancellationToken cancellationToken = default);
}
