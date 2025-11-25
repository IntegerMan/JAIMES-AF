namespace MattEland.Jaimes.Agents.Services;

public interface IAzureOpenAIEmbeddingService
{
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
}

