namespace MattEland.Jaimes.ApiService.Responses;

public record ListGamesResponse
{
    public GameInfoResponse[] Games { get; init; } = [];
}