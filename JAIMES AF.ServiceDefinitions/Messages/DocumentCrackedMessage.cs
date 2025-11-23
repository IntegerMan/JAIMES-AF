namespace MattEland.Jaimes.ServiceDefinitions.Messages;

public class DocumentCrackedMessage
{
    public string DocumentId { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string RelativeDirectory { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public int PageCount { get; set; }
    public DateTime CrackedAt { get; set; }
}

