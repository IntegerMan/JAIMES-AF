namespace MattEland.Jaimes.ServiceDefinitions.Responses;

public record DocumentOperationResponse
{
    public required bool Success { get; init; }
    public required string Message { get; init; }
}