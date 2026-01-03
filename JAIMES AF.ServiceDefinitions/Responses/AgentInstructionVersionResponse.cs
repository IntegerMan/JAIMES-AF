namespace MattEland.Jaimes.ServiceDefinitions.Responses;

public record AgentInstructionVersionResponse
{
    public int Id { get; init; }
    public required string AgentId { get; init; }
    public required string VersionNumber { get; init; }
    public required string Instructions { get; init; }
    public DateTime CreatedAt { get; init; }
    public bool IsActive { get; init; }
    public int GameCount { get; init; }
    public int LatestGameCount { get; init; }
    public int MessageCount { get; init; }
}



