namespace MattEland.Jaimes.ServiceDefinitions.Responses;

public record SearchRulesResponse
{
    public required string Answer { get; init; }
    public required CitationInfo[] Citations { get; init; }
    public required DocumentInfo[] Documents { get; init; }
}

public record CitationInfo
{
    public required string Source { get; init; }
    public required string Text { get; init; }
    public double? Relevance { get; init; }
}

public record DocumentInfo
{
    public required string DocumentId { get; init; }
    public required string Index { get; init; }
    public Dictionary<string, string> Tags { get; init; } = new();
}

