namespace MattEland.Jaimes.ServiceDefinitions.Responses;

public record ChatResponse
{
    public required MessageResponse[] Messages { get; init; }
    public string? ThreadJson { get; init; }
}

