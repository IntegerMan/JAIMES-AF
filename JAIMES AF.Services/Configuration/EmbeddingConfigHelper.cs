namespace MattEland.Jaimes.Services.Configuration;

/// <summary>
/// Centralized helper for embedding configuration constants.
/// This ensures that embedding dimensions match between indexing and searching for vectors to be compatible.
/// </summary>
public static class EmbeddingConfigHelper
{
    /// <summary>
    /// Standard embedding dimensions for text-embedding-3-small model.
    /// This must match between indexing and searching for vectors to be compatible.
    /// </summary>
    public const int EmbeddingDimensions = 1536;

    /// <summary>
    /// Standard max token total for text-embedding-3-small and similar embedding models.
    /// </summary>
    public const int MaxTokenTotal = 8191;
}

