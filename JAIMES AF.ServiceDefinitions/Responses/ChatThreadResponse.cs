namespace MattEland.Jaimes.ServiceDefinitions.Responses;

public record ChatThreadResponse
{
    public required string[] Messages { get; init; }
    public required string ThreadJson { get; init; }
}

