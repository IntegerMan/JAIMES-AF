namespace MattEland.Jaimes.ServiceDefinitions.Messages;

/// <summary>
/// Legacy model - replaced by EF Core entity in JAIMES AF.Repositories.Entities.DocumentChunk
/// This class is kept for backward compatibility with messages.
/// </summary>
[Obsolete("Use MattEland.Jaimes.Repositories.Entities.DocumentChunk instead")]
public class DocumentChunk
{
    public string? Id { get; set; }
    public string ChunkId { get; set; } = string.Empty;
    public string DocumentId { get; set; } = string.Empty;
    public string ChunkText { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? QdrantPointId { get; set; }
}



