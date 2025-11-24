namespace MattEland.Jaimes.ServiceDefinitions.Responses;

public record EmbeddingListResponse
{
    public required EmbeddingListItem[] Embeddings { get; init; } = [];
}

public record EmbeddingListItem
{
    public required string EmbeddingId { get; init; }
    public required string DocumentId { get; init; }
    public required string FileName { get; init; }
    public required string ChunkId { get; init; }
    public required string PreviewText { get; init; }
}




