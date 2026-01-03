namespace MattEland.Jaimes.ServiceDefinitions.Responses;

public record AgentResponse
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Role { get; init; }
}



