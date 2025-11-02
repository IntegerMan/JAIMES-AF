namespace MattEland.Jaimes.Domain;

public class ScenarioDto
{
 public required string Id { get; init; }
 public required string RulesetId { get; init; }
 public string? Description { get; init; }
 public required string Name { get; init; }
}
