namespace MattEland.Jaimes.ApiService.Requests;

public record ChatRequest
{
    public required Guid GameId { get; init; }
    public required string Message { get; init; }
}
