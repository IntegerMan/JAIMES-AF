namespace MattEland.Jaimes.Web.Components.Pages;

public record MessageFeedbackInfo
{
    public required int MessageId { get; init; }
    public required bool IsPositive { get; init; }
    public string? Comment { get; init; }
}

public record MessageToolCallInfo
{
    public required int Id { get; init; }
    public required int MessageId { get; init; }
    public required string ToolName { get; init; }
    public string? InputJson { get; init; }
    public string? OutputJson { get; init; }
    public required DateTime CreatedAt { get; init; }
}
