namespace MattEland.Jaimes.ServiceDefinitions.Requests;

public record DeleteEmbeddingRequest
{
    public required string EmbeddingId { get; init; }
}



