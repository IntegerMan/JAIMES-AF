namespace MattEland.Jaimes.ServiceDefinitions.Responses;

public record InitialMessageResponse
{
    public required string Message { get; init; }
    public required string ThreadJson { get; init; }
}