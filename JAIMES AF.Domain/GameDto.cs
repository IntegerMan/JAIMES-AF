namespace MattEland.Jaimes.Domain;

public class GameDto
{
    public required Guid GameId { get; init; }
    public required string RulesetId { get; init; }
    public required string ScenarioId { get; init; }
    public required string PlayerId { get; init; }
    public MessageDto[]? Messages { get; init; }

    // Names for convenience in responses
    public required string ScenarioName { get; init; }
    public required string RulesetName { get; init; }
    public required string PlayerName { get; init; }
}
