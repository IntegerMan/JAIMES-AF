namespace MattEland.Jaimes.ServiceDefinitions.Responses;

public record BackfillEmbeddingsResponse
{
    public required int DocumentsQueued { get; init; }
    public required int TotalUnprocessed { get; init; }
    public required string[] DocumentIds { get; init; } = [];
}