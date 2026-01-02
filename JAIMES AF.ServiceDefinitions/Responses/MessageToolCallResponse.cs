namespace MattEland.Jaimes.ServiceDefinitions.Responses;

public record MessageToolCallResponse
{
    public required int Id { get; init; }
    public required int MessageId { get; init; }
    public required string ToolName { get; init; }
    public string? InputJson { get; init; }
    public string? OutputJson { get; init; }
    public required DateTime CreatedAt { get; init; }
    public int? InstructionVersionId { get; init; }
}


