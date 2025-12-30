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
    // Sentiment analysis result: -1 (negative), 0 (neutral), 1 (positive), null (not analyzed)
    public int? Sentiment { get; init; }
    public string? ModelName { get; init; }
    public string? ModelProvider { get; init; }
    public string? ModelEndpoint { get; init; }
}