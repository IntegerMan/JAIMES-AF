namespace MattEland.Jaimes.ServiceDefinitions.Responses;

public record SearchRulesResponse
{
    public required SearchRuleResult[] Results { get; init; }
}

public record SearchRuleResult
{
    public required string Text { get; init; }
    public required string DocumentId { get; init; }
    public required string EmbeddingId { get; init; }
    public required string ChunkId { get; init; }
    public required double Relevancy { get; init; }
}

