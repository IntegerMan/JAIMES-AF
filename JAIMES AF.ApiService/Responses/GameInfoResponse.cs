namespace MattEland.Jaimes.ApiService.Responses;

public record GameInfoResponse
{
    public required Guid GameId { get; init; }
}