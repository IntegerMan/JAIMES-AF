namespace MattEland.Jaimes.ServiceDefinitions.Requests;

public record GenerateInitialMessageRequest
{
    public required Guid GameId { get; init; }
    public required string SystemPrompt { get; init; }
    public required string NewGameInstructions { get; init; }
    public required string PlayerName { get; init; }
    public string? PlayerDescription { get; init; }
}