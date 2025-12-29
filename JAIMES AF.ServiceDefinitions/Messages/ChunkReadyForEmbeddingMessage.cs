namespace MattEland.Jaimes.ServiceDefinitions.Messages;

public class ChunkReadyForEmbeddingMessage
{
    public string ChunkId { get; set; } = string.Empty;
    public string ChunkText { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public string DocumentId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string RelativeDirectory { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public int PageCount { get; set; }
    public int? PageNumber { get; set; }
    public DateTime CrackedAt { get; set; }
    public int TotalChunks { get; set; }
    public string DocumentKind { get; set; } = DocumentKinds.Sourcebook;
    public string RulesetId { get; set; } = string.Empty;
}



