namespace MattEland.Jaimes.Repositories.Entities;

public class RagSearchQuery
{
    public Guid Id { get; set; }
    public required string Query { get; set; }
    public string? RulesetId { get; set; }
    public required string IndexName { get; set; }
    public string? FilterJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public ICollection<RagSearchResultChunk> ResultChunks { get; set; } = new List<RagSearchResultChunk>();
}