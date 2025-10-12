namespace MattEland.Jaimes.ApiService.Responses;

public class GameStateResponse
{
    public required Guid GameId { get; init; }
    public required MessageResponse[] Messages { get; init; }
}
