namespace MattEland.Jaimes.ServiceDefinitions.Responses;

public record RulesetResponse
{
    public required string Id { get; init; }
    public required string Name { get; init; }
}

