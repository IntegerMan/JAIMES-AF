namespace MattEland.Jaimes.ServiceDefinitions.Requests;

public record SearchRulesRequest
{
    public required string Query { get; init; }
    public string? RulesetId { get; init; }
    public bool StoreResults { get; init; } = true;
}

