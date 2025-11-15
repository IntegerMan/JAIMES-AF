namespace MattEland.Jaimes.ServiceDefinitions.Responses;

public record JaimesChatResponse
{
    public required MessageResponse[] Messages { get; init; }
    public string? ThreadJson { get; init; }
}

