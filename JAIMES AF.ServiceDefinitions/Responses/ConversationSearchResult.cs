namespace MattEland.Jaimes.ServiceDefinitions.Responses;

public record ConversationSearchResult
{
    public required MessageResponse MatchedMessage { get; init; }
    public MessageResponse? PreviousMessage { get; init; }
    public MessageResponse? NextMessage { get; init; }
    public required double Relevancy { get; init; }
}

public record ConversationSearchResponse
{
    public required ConversationSearchResult[] Results { get; init; }
}

