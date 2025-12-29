namespace MattEland.Jaimes.ServiceDefinitions.Responses;

public record MessageFeedbackResponse
{
    public required int Id { get; init; }
    public required int MessageId { get; init; }
    public required bool IsPositive { get; init; }
    public string? Comment { get; init; }
    public required DateTime CreatedAt { get; init; }
    public int? InstructionVersionId { get; init; }
}

