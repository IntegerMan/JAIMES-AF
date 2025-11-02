namespace MattEland.Jaimes.ServiceDefinitions.Responses;

public record ListGamesResponse
{
 public required GameInfoResponse[] Games { get; init; } = [];
}
