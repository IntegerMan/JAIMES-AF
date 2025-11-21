using Microsoft.KernelMemory;

namespace MattEland.Jaimes.Services.Configuration;

/// <summary>
/// Centralized helper for creating AzureOpenAIConfig instances with consistent embedding configuration.
/// This ensures that embedding dimensions and max token total match between the Indexer and Services projects.
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

    /// <summary>
    /// Creates an AzureOpenAIConfig instance for embedding generation with standardized settings.
    /// </summary>
    /// <param name="apiKey">The Azure OpenAI API key</param>
    /// <param name="endpoint">The Azure OpenAI endpoint URL (will be normalized)</param>
    /// <param name="deployment">The deployment name (e.g., "text-embedding-3-small" or "text-embedding-3-small-global")</param>
    /// <returns>A configured AzureOpenAIConfig instance for embeddings</returns>
    public static AzureOpenAIConfig CreateEmbeddingConfig(string apiKey, string endpoint, string deployment)
    {
        // Normalize endpoint URL - remove trailing slash to avoid 404 errors
        string normalizedEndpoint = endpoint.TrimEnd('/');

        return new AzureOpenAIConfig
        {
            APIKey = apiKey,
            Auth = AzureOpenAIConfig.AuthTypes.APIKey,
            Endpoint = normalizedEndpoint,
            Deployment = deployment,
            EmbeddingDimensions = EmbeddingDimensions,
            MaxTokenTotal = MaxTokenTotal,
        };
    }
}

