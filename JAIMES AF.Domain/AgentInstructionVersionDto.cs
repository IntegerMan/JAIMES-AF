namespace MattEland.Jaimes.Domain;

public class AgentInstructionVersionDto
{
    public int Id { get; init; }
    public required string AgentId { get; init; }
    public required string VersionNumber { get; init; }
    public required string Instructions { get; init; }
    public DateTime CreatedAt { get; init; }
    public bool IsActive { get; init; }
}
