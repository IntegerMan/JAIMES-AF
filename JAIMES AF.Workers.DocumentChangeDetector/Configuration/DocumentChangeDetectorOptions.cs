namespace MattEland.Jaimes.Workers.DocumentScanner.Configuration;

public class DocumentScannerOptions
{
    public string? ContentDirectory { get; set; }
    public List<string> SupportedExtensions { get; set; } = [".pdf"];
}




