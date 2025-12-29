namespace MattEland.Jaimes.ServiceLayer.Services;

using MattEland.Jaimes.Domain;
using MattEland.Jaimes.Repositories.Entities;

public static class AgentInstructionVersionMapper
{
    public static AgentInstructionVersionDto ToDto(this AgentInstructionVersion version)
    {
        return new AgentInstructionVersionDto
        {
            Id = version.Id,
            AgentId = version.AgentId,
            VersionNumber = version.VersionNumber,
            Instructions = version.Instructions,
            CreatedAt = version.CreatedAt,
            IsActive = version.IsActive
        };
    }

    public static AgentInstructionVersionDto[] ToDto(this IEnumerable<AgentInstructionVersion> versions)
    {
        return versions.Select(ToDto).ToArray();
    }
}

