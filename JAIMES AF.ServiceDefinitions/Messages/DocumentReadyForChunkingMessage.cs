namespace MattEland.Jaimes.ServiceDefinitions.Messages;

public class DocumentReadyForChunkingMessage
{
    public string DocumentId { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string RelativeDirectory { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public int PageCount { get; set; }
    public DateTime CrackedAt { get; set; }
    public string DocumentKind { get; set; } = DocumentKinds.Sourcebook;
    public string RulesetId { get; set; } = string.Empty;
}