namespace MattEland.Jaimes.ServiceLayer.Services;

using MattEland.Jaimes.Domain;
using MattEland.Jaimes.Repositories.Entities;

public static class ScenarioAgentMapper
{
    public static ScenarioAgentDto ToDto(this ScenarioAgent scenarioAgent)
    {
        return new ScenarioAgentDto
        {
            ScenarioId = scenarioAgent.ScenarioId,
            AgentId = scenarioAgent.AgentId,
            InstructionVersionId = scenarioAgent.InstructionVersionId
        };
    }

    public static ScenarioAgentDto[] ToDto(this IEnumerable<ScenarioAgent> scenarioAgents)
    {
        return scenarioAgents.Select(ToDto).ToArray();
    }
}


