using MattEland.Jaimes.DocumentProcessing.Options;

namespace MattEland.Jaimes.Indexer.Configuration;

public class IndexerOptions : DocumentScanOptions
{
    public required string VectorDbConnectionString { get; init; }
    public required string OllamaEndpoint { get; init; }
    public required string OllamaModel { get; init; }
}

