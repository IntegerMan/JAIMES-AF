namespace MattEland.Jaimes.ServiceDefinitions.Requests;

public record ConversationSearchRequest
{
    public required Guid GameId { get; init; }
    public required string Query { get; init; }
    public int Limit { get; init; } = 5;
}

