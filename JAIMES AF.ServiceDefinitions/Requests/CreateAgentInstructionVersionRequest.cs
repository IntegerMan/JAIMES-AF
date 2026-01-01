namespace MattEland.Jaimes.ServiceDefinitions.Requests;

public record CreateAgentInstructionVersionRequest
{
    public required string VersionNumber { get; init; }
    public required string Instructions { get; init; }
}


