namespace MattEland.Jaimes.Indexer.Configuration;

public class IndexerOptions
{
    public required string SourceDirectory { get; init; }
    public required string VectorDbConnectionString { get; init; }
    public required string OllamaEndpoint { get; init; }
    public required string OllamaModel { get; init; }
    public List<string> SupportedExtensions { get; init; } = [".txt", ".md", ".pdf", ".docx"];
}

