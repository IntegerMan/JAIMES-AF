namespace MattEland.Jaimes.ServiceDefinitions.Responses;

public record GameStateResponse
{
    public required Guid GameId { get; init; }
    public required MessageResponse[] Messages { get; init; }
}
