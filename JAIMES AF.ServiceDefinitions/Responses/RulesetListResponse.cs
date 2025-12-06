namespace MattEland.Jaimes.ServiceDefinitions.Responses;

public record RulesetInfoResponse
{
    public required string Id { get; init; }
    public required string Name { get; init; }
}

public record RulesetListResponse
{
    public required RulesetInfoResponse[] Rulesets { get; init; }
}