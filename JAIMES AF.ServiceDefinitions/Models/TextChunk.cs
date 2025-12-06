namespace MattEland.Jaimes.ServiceDefinitions.Models;

public class TextChunk
{
    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public int Index { get; set; }
    public string SourceDocumentId { get; set; } = string.Empty;
    public float[]? Embedding { get; set; }
}