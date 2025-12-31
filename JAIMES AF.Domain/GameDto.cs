namespace MattEland.Jaimes.Domain;

public class GameDto
{
    public required Guid GameId { get; init; }
    public string? Title { get; init; }
    public MessageDto[]? Messages { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? LastPlayedAt { get; init; }

    // Composed DTOs
    public required RulesetDto Ruleset { get; init; }
    public required ScenarioDto Scenario { get; init; }
    public required PlayerDto Player { get; init; }
}