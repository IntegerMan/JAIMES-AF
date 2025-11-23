namespace MattEland.Jaimes.Workers.DocumentEmbeddings.Configuration;

public class EmbeddingWorkerOptions
{
    public string? OllamaModel { get; set; } = "nomic-embed-text";
    public string CollectionName { get; set; } = "document-embeddings";
    public int EmbeddingDimensions { get; set; } = 768; // nomic-embed-text produces 768-dimensional vectors
}




