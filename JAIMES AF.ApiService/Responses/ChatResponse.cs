namespace MattEland.Jaimes.ApiService.Responses;

public record ChatResponse
{
    public required Guid GameId { get; init; }
    public required MessageResponse[] Messages { get; init; }

}
