namespace MattEland.Jaimes.Domain;

public class MessageDto
{
    public int Id { get; init; }
    public required string Text { get; init; }
    public string? PlayerId { get; init; }
    public required string ParticipantName { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? AgentId { get; init; }
    public int? InstructionVersionId { get; init; }
    public int? Sentiment { get; init; }
}