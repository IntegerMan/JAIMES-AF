using MattEland.Jaimes.Domain;

namespace MattEland.Jaimes.ServiceDefinitions.Services;

public interface IAgentsService
{
    Task<AgentDto[]> GetAgentsAsync(CancellationToken cancellationToken = default);
    Task<AgentDto?> GetAgentAsync(string id, CancellationToken cancellationToken = default);
    Task<AgentDto> CreateAgentAsync(string name, string role, string instructions, CancellationToken cancellationToken = default);
    Task<AgentDto> UpdateAgentAsync(string id, string name, string role, CancellationToken cancellationToken = default);
    Task DeleteAgentAsync(string id, CancellationToken cancellationToken = default);
}
