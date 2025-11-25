namespace MattEland.Jaimes.Workers.DocumentChunking.Configuration;

public class DocumentChunkingOptions
{
    public string? OllamaModel { get; set; } = "nomic-embed-text";
    
    // SemanticChunker configuration
    public int TokenLimit { get; set; } = 512; // Max tokens per chunk (with 10% safety margin)
    public int BufferSize { get; set; } = 1; // Sentences added before/after current sentence during embedding
    public string ThresholdType { get; set; } = "Percentile"; // Options: Percentile, StandardDeviation, InterQuartile, Gradient
    public double ThresholdAmount { get; set; } = 95.0; // E.g., 95 for Percentile, 3.0 for StandardDeviation
    public int? TargetChunkCount { get; set; } = null; // Overrides thresholds to hit exact chunk count (optional)
    public int MinChunkChars { get; set; } = 0; // Skip chunks shorter than this
}

