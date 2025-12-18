using MattEland.Jaimes.Domain;

namespace MattEland.Jaimes.ServiceDefinitions.Services;

public interface IAgentInstructionVersionsService
{
    Task<AgentInstructionVersionDto[]> GetInstructionVersionsAsync(string agentId, CancellationToken cancellationToken = default);
    Task<AgentInstructionVersionDto?> GetInstructionVersionAsync(int id, CancellationToken cancellationToken = default);
    Task<AgentInstructionVersionDto> CreateInstructionVersionAsync(string agentId, string versionNumber, string instructions, CancellationToken cancellationToken = default);
    Task<AgentInstructionVersionDto?> GetActiveInstructionVersionAsync(string agentId, CancellationToken cancellationToken = default);
    Task<AgentInstructionVersionDto> UpdateInstructionVersionAsync(int id, string versionNumber, string instructions, bool? isActive, CancellationToken cancellationToken = default);
}
