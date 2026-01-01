namespace MattEland.Jaimes.ServiceDefinitions.Responses;

public record GameStateResponse
{
    public required Guid GameId { get; init; }
    public string? Title { get; init; }
    public required MessageResponse[] Messages { get; init; }
    public required string RulesetId { get; init; }
    public required string ScenarioId { get; init; }
    public required string PlayerId { get; init; }

    // Names for convenience in responses
    public required string ScenarioName { get; init; }
    public required string RulesetName { get; init; }
    public required string PlayerName { get; init; }

    // Dates
    public DateTime CreatedAt { get; init; }
    public DateTime? LastPlayedAt { get; init; }

    // Thread JSON for restoring AgentThread state
    public string? ThreadJson { get; init; }

    public string? AgentId { get; init; }
    public int? InstructionVersionId { get; init; }
}