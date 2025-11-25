using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace MattEland.Jaimes.Workers.DocumentChunking.Services;

/// <summary>
/// Adapter that wraps IOllamaEmbeddingService to implement IEmbeddingGenerator for SemanticChunker.NET
/// </summary>
public class OllamaEmbeddingGeneratorAdapter(
    IOllamaEmbeddingService ollamaEmbeddingService,
    ILogger<OllamaEmbeddingGeneratorAdapter> logger) : IEmbeddingGenerator<string, Embedding<float>>, IServiceProvider, IDisposable
{
    public async Task<Embedding<float>> GenerateEmbeddingAsync(string input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentException("Input cannot be null or empty", nameof(input));
        }

        float[] embeddingArray = await ollamaEmbeddingService.GenerateEmbeddingAsync(input, cancellationToken);

        Embedding<float> embedding = new(embeddingArray);

        return embedding;
    }

    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> inputs, 
        EmbeddingGenerationOptions? options = null, 
        CancellationToken cancellationToken = default)
    {
        List<Embedding<float>> embeddings = new();
        
        // Process embeddings sequentially (Ollama may not support batching)
        foreach (string input in inputs)
        {
            Embedding<float> embedding = await GenerateEmbeddingAsync(input, cancellationToken);
            embeddings.Add(embedding);
        }

        return new GeneratedEmbeddings<Embedding<float>>(embeddings);
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        // Implementation for IEmbeddingGenerator.GetService
        return null;
    }

    object? IServiceProvider.GetService(Type serviceType)
    {
        // Implementation for IServiceProvider.GetService
        return null;
    }

    public void Dispose()
    {
        // Nothing to dispose
    }
}

