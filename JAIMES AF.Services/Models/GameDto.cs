namespace MattEland.Jaimes.Services.Models;

public class GameDto
{
    public required Guid GameId { get; init; }
    public required string RulesetId { get; init; }
    public required string ScenarioId { get; init; }
    public required string PlayerId { get; init; }
    public required MessageDto[] Messages { get; init; }
}
