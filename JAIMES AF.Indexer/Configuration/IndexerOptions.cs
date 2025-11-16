namespace MattEland.Jaimes.Indexer.Configuration;

public class IndexerOptions
{
    public required string SourceDirectory { get; init; }
    public string VectorDbConnectionString { get; init; } = "Data Source=km_vector_store.db";
    public required string OpenAiEndpoint { get; init; }
    public required string OpenAiApiKey { get; init; }
    public required string OpenAiDeployment { get; init; }
    public List<string> SupportedExtensions { get; init; } = new() { ".txt", ".md", ".pdf", ".docx" };
    public bool Recursive { get; init; } = true;
}

