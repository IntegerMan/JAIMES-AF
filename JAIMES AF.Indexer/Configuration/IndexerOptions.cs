namespace MattEland.Jaimes.Indexer.Configuration;

public class IndexerOptions
{
    public required string SourceDirectory { get; init; }
    public required string VectorDbConnectionString { get; init; }
    public required string OpenAiEndpoint { get; init; }
    public required string OpenAiApiKey { get; init; }
    public required string OpenAiDeployment { get; init; }
    public List<string> SupportedExtensions { get; init; } = [".txt", ".md", ".pdf", ".docx"];
    public required string DocIntelApiKey { get; set; }
    public required string DocIntelEndpoint { get; set; }
}

