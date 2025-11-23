namespace MattEland.Jaimes.Workers.DocumentChangeDetector.Configuration;

public class DocumentChangeDetectorOptions
{
    public string? ContentDirectory { get; set; }
    public List<string> SupportedExtensions { get; set; } = [".pdf"];
}




