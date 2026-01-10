namespace MattEland.Jaimes.ServiceDefinitions.Requests;

public record ChatRequest
{
    public required Guid GameId { get; init; }
    public required string Message { get; init; }
    public Guid? TrackingGuid { get; init; }
}