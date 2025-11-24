namespace MattEland.Jaimes.Workers.DocumentEmbeddings.Configuration;

public class EmbeddingWorkerOptions
{
    public string? OllamaModel { get; set; } = "nomic-embed-text";
    public string CollectionName { get; set; } = "document-embeddings";
    public int EmbeddingDimensions { get; set; } = 768; // nomic-embed-text produces 768-dimensional vectors
    public int HttpClientTimeoutMinutes { get; set; } = 15; // Default 15 minutes for large document embeddings
    
    // SemanticChunker configuration
    public int TokenLimit { get; set; } = 512; // Max tokens per chunk (with 10% safety margin)
    public int BufferSize { get; set; } = 1; // Sentences added before/after current sentence during embedding
    public string ThresholdType { get; set; } = "Percentile"; // Options: Percentile, StandardDeviation, InterQuartile, Gradient
    public double ThresholdAmount { get; set; } = 95.0; // E.g., 95 for Percentile, 3.0 for StandardDeviation
    public int? TargetChunkCount { get; set; } = null; // Overrides thresholds to hit exact chunk count (optional)
    public int MinChunkChars { get; set; } = 0; // Skip chunks shorter than this
}




