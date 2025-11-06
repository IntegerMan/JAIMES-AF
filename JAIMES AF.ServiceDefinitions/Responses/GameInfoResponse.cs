namespace MattEland.Jaimes.ServiceDefinitions.Responses;

public record GameInfoResponse
{
    public required Guid GameId { get; init; }

    // Scenario
    public required string ScenarioId { get; init; }
    public required string ScenarioName { get; init; }

    // Ruleset
    public required string RulesetId { get; init; }
    public required string RulesetName { get; init; }

    // Player
    public required string PlayerId { get; init; }
    public required string PlayerName { get; init; }
}
