namespace MattEland.Jaimes.Workers.DocumentCracker.Configuration;

public class DocumentCrackerWorkerOptions
{
    public string? SourceDirectory { get; set; }
    public List<string> SupportedExtensions { get; set; } = [".pdf"];
}




