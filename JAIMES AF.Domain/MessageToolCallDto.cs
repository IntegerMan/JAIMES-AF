namespace MattEland.Jaimes.Domain;

public class MessageToolCallDto
{
    public int Id { get; init; }
    public int MessageId { get; init; }
    public required string ToolName { get; init; }
    public string? InputJson { get; init; }
    public string? OutputJson { get; init; }
    public DateTime CreatedAt { get; init; }
    public int? InstructionVersionId { get; init; }
}



