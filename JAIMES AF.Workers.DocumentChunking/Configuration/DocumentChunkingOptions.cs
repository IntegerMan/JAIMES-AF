namespace MattEland.Jaimes.Workers.DocumentChunking.Configuration;

public class DocumentChunkingOptions
{
    public string? OllamaModel { get; set; } = "nomic-embed-text";
    
    // Chunking strategy selection
    public string ChunkingStrategy { get; set; } = "SemanticSlicer"; // Options: "SemanticChunker" or "SemanticSlicer"
    
    // SemanticChunker configuration
    public int TokenLimit { get; set; } = 1024; // Max tokens per chunk - targeting paragraph-sized chunks (small to medium paragraphs)
    public int BufferSize { get; set; } = 2; // Sentences added before/after current sentence during embedding (increased for better context)
    public string ThresholdType { get; set; } = "Percentile"; // Options: Percentile, StandardDeviation, InterQuartile, Gradient
    public double ThresholdAmount { get; set; } = 90.0; // E.g., 90 for Percentile (lower = larger chunks), 3.0 for StandardDeviation
    public int? TargetChunkCount { get; set; } = null; // Overrides thresholds to hit exact chunk count (optional)
    
    // SemanticSlicer configuration
    public int SemanticSlicerMaxChunkTokenCount { get; set; } = 1000; // Max tokens per chunk for SemanticSlicer
    public string SemanticSlicerSeparators { get; set; } = "Text"; // Options: "Text", "Markdown", "Html", or custom
    public bool SemanticSlicerStripHtml { get; set; } = false; // Whether to strip HTML tags from chunks
    
    // Common configuration
    public int MinChunkChars { get; set; } = 100; // Skip chunks shorter than this (filters out very small fragments)
    
    // Qdrant configuration
    public string CollectionName { get; set; } = "document-embeddings";
}

