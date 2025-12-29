namespace MattEland.Jaimes.ServiceLayer.Services;

using MattEland.Jaimes.Domain;
using MattEland.Jaimes.Repositories.Entities;

public static class AgentMapper
{
    public static AgentDto ToDto(this Agent agent)
    {
        return new AgentDto
        {
            Id = agent.Id,
            Name = agent.Name,
            Role = agent.Role
        };
    }

    public static AgentDto[] ToDto(this IEnumerable<Agent> agents)
    {
        return agents.Select(ToDto).ToArray();
    }
}

