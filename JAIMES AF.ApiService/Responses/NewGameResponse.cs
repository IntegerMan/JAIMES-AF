namespace MattEland.Jaimes.ApiService.Responses;

public class NewGameResponse
{
    public required Guid GameId { get; init; }
    public required MessageResponse[] Messages { get; init; }
}