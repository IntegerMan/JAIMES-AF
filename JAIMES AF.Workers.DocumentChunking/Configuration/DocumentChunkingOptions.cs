namespace MattEland.Jaimes.Workers.DocumentChunking.Configuration;

public class DocumentChunkingOptions
{
    public string? OllamaModel { get; set; } = "nomic-embed-text";
    
    // SemanticChunker configuration
    public int TokenLimit { get; set; } = 1024; // Max tokens per chunk - targeting paragraph-sized chunks (small to medium paragraphs)
    public int BufferSize { get; set; } = 2; // Sentences added before/after current sentence during embedding (increased for better context)
    public string ThresholdType { get; set; } = "Percentile"; // Options: Percentile, StandardDeviation, InterQuartile, Gradient
    public double ThresholdAmount { get; set; } = 90.0; // E.g., 90 for Percentile (lower = larger chunks), 3.0 for StandardDeviation
    public int? TargetChunkCount { get; set; } = null; // Overrides thresholds to hit exact chunk count (optional)
    public int MinChunkChars { get; set; } = 100; // Skip chunks shorter than this (filters out very small fragments)
    
    // Qdrant configuration
    public string CollectionName { get; set; } = "document-embeddings";
    public int EmbeddingDimensions { get; set; } = 768; // nomic-embed-text produces 768-dimensional vectors
}

