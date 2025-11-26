namespace MattEland.Jaimes.Agents.Services;

public interface IQdrantRulesStore
{
    Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default);
    Task StoreRuleAsync(string ruleId, float[] embedding, Dictionary<string, string> metadata, CancellationToken cancellationToken = default);
    Task<List<RuleSearchResult>> SearchRulesAsync(float[] queryEmbedding, string? rulesetId, int limit, CancellationToken cancellationToken = default);
}

public record RuleSearchResult
{
    public required string RuleId { get; init; }
    public required string Title { get; init; }
    public required string Content { get; init; }
    public required string RulesetId { get; init; }
    public required float Score { get; init; }
}

