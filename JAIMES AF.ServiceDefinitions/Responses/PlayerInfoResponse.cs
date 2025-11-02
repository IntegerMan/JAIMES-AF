namespace MattEland.Jaimes.ServiceDefinitions.Responses;

public record PlayerInfoResponse
{
 public required string Id { get; init; }
 public required string RulesetId { get; init; }
 public string? Description { get; init; }
 public required string Name { get; init; }
}
