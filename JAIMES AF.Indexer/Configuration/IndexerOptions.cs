namespace MattEland.Jaimes.Indexer.Configuration;

public class IndexerOptions
{
    public required string SourceDirectory { get; init; }
    public required string VectorDbConnectionString { get; init; }
    public required string OpenAiEndpoint { get; init; }
    public required string OpenAiApiKey { get; init; }
    public required string OpenAiDeployment { get; init; }
    public string ChangeTrackingFile { get; init; } = "indexer_tracking.json";
    public List<string> SupportedExtensions { get; init; } = new() { ".txt", ".md", ".pdf", ".docx" };
    public bool Recursive { get; init; } = true;
}

