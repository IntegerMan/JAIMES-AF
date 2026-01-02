namespace MattEland.Jaimes.ServiceDefinitions.Requests;

public record UpdateAgentRequest
{
    public required string Name { get; init; }
    public required string Role { get; init; }
}


