namespace MattEland.Jaimes.ServiceDefinitions.Services;

public class JaimesChatOptions
{
    public required string Endpoint { get; init; }
    public required string ApiKey { get; init; }
    /// <summary>
    /// Deployment name for text generation (chat/completion) models
    /// </summary>
    public required string TextGenerationDeployment { get; init; }
    /// <summary>
    /// Deployment name for embedding models (vector embeddings)
    /// </summary>
    public required string EmbeddingDeployment { get; init; }
}
