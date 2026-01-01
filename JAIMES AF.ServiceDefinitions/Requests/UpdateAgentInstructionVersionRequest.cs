namespace MattEland.Jaimes.ServiceDefinitions.Requests;

public record UpdateAgentInstructionVersionRequest
{
    public required string VersionNumber { get; init; }
    public required string Instructions { get; init; }
    public bool? IsActive { get; init; }
}


