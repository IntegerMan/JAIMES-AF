namespace MattEland.Jaimes.ServiceDefinitions.Responses;

public record ScenarioResponse
{
    public required string Id { get; init; }
    public required string RulesetId { get; init; }
    public string? Description { get; init; }
    public required string Name { get; init; }
    public required string SystemPrompt { get; init; }
    public required string NewGameInstructions { get; init; }
    public string? InitialGreeting { get; init; }
}