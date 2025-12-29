namespace MattEland.Jaimes.Domain;

public class MessageFeedbackDto
{
    public int Id { get; init; }
    public int MessageId { get; init; }
    public bool IsPositive { get; init; }
    public string? Comment { get; init; }
    public DateTime CreatedAt { get; init; }
    public int? InstructionVersionId { get; init; }
}

