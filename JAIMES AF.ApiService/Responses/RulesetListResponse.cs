namespace MattEland.Jaimes.ApiService.Responses;

public record RulesetListResponse
{
 public required RulesetInfoResponse[] Rulesets { get; init; }
}

public record RulesetInfoResponse
{
 public required string Id { get; init; }
 public required string Name { get; init; }
}
