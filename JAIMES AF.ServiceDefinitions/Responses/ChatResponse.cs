namespace MattEland.Jaimes.ServiceDefinitions.Responses;

public record ChatResponse
{
    public required MessageResponse[] Messages { get; init; }
}

