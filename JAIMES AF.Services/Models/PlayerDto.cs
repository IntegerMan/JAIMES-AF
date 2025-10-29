namespace MattEland.Jaimes.Services.Models;

public class PlayerDto
{
 public required string Id { get; init; }
 public required string RulesetId { get; init; }
 public string? Description { get; init; }
}
