namespace MattEland.Jaimes.ServiceDefinitions.Requests;

public record CreateAgentRequest
{
    public required string Name { get; init; }
    public required string Role { get; init; }
}
