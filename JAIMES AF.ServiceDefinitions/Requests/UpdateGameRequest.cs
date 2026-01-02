namespace MattEland.Jaimes.ServiceDefinitions.Requests;

public record UpdateGameRequest
{
    public string? Title { get; init; }
    public string? AgentId { get; init; }
    public int? InstructionVersionId { get; init; }
}
