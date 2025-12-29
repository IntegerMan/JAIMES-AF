namespace MattEland.Jaimes.ServiceDefinitions.Responses;

public record AgentInstructionVersionListResponse
{
    public required AgentInstructionVersionResponse[] InstructionVersions { get; init; }
}

