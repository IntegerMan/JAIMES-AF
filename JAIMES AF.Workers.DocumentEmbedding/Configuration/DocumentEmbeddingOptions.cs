namespace MattEland.Jaimes.Workers.DocumentEmbedding.Configuration;

public class DocumentEmbeddingOptions
{
    public string? OllamaModel { get; set; } = "nomic-embed-text";
    
    // Qdrant configuration
    public string CollectionName { get; set; } = "document-embeddings";
    public int EmbeddingDimensions { get; set; } = 768; // nomic-embed-text produces 768-dimensional vectors
}

