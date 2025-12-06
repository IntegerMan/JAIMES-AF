namespace MattEland.Jaimes.DocumentProcessing.Options;

public class DocumentScanOptions
{
    public string? SourceDirectory { get; set; }
    public List<string> SupportedExtensions { get; set; } = [".txt", ".md", ".pdf", ".docx"];
}