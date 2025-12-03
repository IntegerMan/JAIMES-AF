namespace MattEland.Jaimes.ServiceDefinitions.Messages;

/// <summary>
/// Legacy model - replaced by EF Core entity in JAIMES AF.Repositories.Entities.CrackedDocument
/// This class is kept for backward compatibility with messages.
/// </summary>
[Obsolete("Use MattEland.Jaimes.Repositories.Entities.CrackedDocument instead")]
public class CrackedDocument
{
    public string? Id { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string RelativeDirectory { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CrackedAt { get; set; } = DateTime.UtcNow;
    public long FileSize { get; set; }
    public int PageCount { get; set; }
    public bool IsProcessed { get; set; } = false;
    public int TotalChunks { get; set; } = 0;
    public int ProcessedChunkCount { get; set; } = 0;
    public string DocumentKind { get; set; } = "Sourcebook";
    public string RulesetId { get; set; } = string.Empty;
}




