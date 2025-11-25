namespace MattEland.Jaimes.Workers.DocumentChunking.Services;

public interface IOllamaEmbeddingService
{
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
}

