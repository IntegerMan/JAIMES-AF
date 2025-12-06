namespace MattEland.Jaimes.ServiceDefinitions.Requests;

public record DeleteDocumentRequest
{
    public required string FilePath { get; init; }
}