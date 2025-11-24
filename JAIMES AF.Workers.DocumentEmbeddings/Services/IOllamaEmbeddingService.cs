namespace MattEland.Jaimes.Workers.DocumentEmbeddings.Services;

public interface IOllamaEmbeddingService
{
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
}







