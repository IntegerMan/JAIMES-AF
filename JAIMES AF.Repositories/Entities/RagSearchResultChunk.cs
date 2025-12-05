namespace MattEland.Jaimes.Repositories.Entities;

public class RagSearchResultChunk
{
    public Guid Id { get; set; }
    public Guid RagSearchQueryId { get; set; }
    public required string ChunkId { get; set; }
    public required string DocumentId { get; set; }
    public required string DocumentName { get; set; }
    public required string EmbeddingId { get; set; }
    public required string RulesetId { get; set; }
    public required double Relevancy { get; set; }
    public RagSearchQuery? RagSearchQuery { get; set; }
}
