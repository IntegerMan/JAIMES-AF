namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Response DTO for test case data.
/// </summary>
public record TestCaseResponse
{
    public required int Id { get; init; }
    public required int MessageId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required bool IsActive { get; init; }

    // Contextual information from the referenced message
    public required Guid GameId { get; init; }
    public string? GameTitle { get; init; }
    public string? MessageText { get; init; }
    public string? AgentId { get; init; }
    public string? AgentName { get; init; }

    // Run statistics
    public int RunCount { get; init; }
}
