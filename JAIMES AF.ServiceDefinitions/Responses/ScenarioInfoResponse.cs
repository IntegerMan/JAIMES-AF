namespace MattEland.Jaimes.ServiceDefinitions.Responses;

public record ScenarioInfoResponse
{
    public required string Id { get; init; }
    public string? Description { get; init; }
    public required string Name { get; init; }
    public required string RulesetId { get; init; }
}