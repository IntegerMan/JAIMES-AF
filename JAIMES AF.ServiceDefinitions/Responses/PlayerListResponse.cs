namespace MattEland.Jaimes.ServiceDefinitions.Responses;

public record PlayerListResponse
{
    public required PlayerInfoResponse[] Players { get; init; }
}