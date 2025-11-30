namespace MattEland.Jaimes.Workers.DocumentEmbedding.Configuration;

public class DocumentEmbeddingOptions
{
    public string? OllamaModel { get; set; } = "nomic-embed-text";
    
    // Qdrant configuration
    public string CollectionName { get; set; } = "document-embeddings";
}

