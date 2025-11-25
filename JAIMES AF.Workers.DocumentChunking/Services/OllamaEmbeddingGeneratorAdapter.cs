using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MattEland.Jaimes.Workers.DocumentChunking.Configuration;

namespace MattEland.Jaimes.Workers.DocumentChunking.Services;

/// <summary>
/// Adapter that implements IEmbeddingGenerator for SemanticChunker.NET by calling Ollama's embedding API directly
/// </summary>
public class OllamaEmbeddingGeneratorAdapter(
    HttpClient httpClient,
    OllamaEmbeddingOptions options,
    ILogger<OllamaEmbeddingGeneratorAdapter> logger) : IEmbeddingGenerator<string, Embedding<float>>, IServiceProvider, IDisposable
{
    public async Task<Embedding<float>> GenerateEmbeddingAsync(string input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentException("Input cannot be null or empty", nameof(input));
        }

        string requestUrl = $"{options.Endpoint}/api/embeddings";
        
        OllamaEmbeddingRequest request = new()
        {
            Model = options.Model,
            Prompt = input
        };

        logger.LogDebug("Generating embedding for text (length: {Length}) using model {Model}", input.Length, options.Model);

        HttpResponseMessage response = await httpClient.PostAsJsonAsync(requestUrl, request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("Failed to generate embedding. Status: {StatusCode}, Response: {Response}", 
                response.StatusCode, errorContent);
            response.EnsureSuccessStatusCode();
        }

        OllamaEmbeddingResponse? embeddingResponse = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(
            cancellationToken: cancellationToken);

        if (embeddingResponse?.Embedding == null || embeddingResponse.Embedding.Length == 0)
        {
            throw new InvalidOperationException("Received empty embedding from Ollama");
        }

        logger.LogDebug("Generated embedding with {Dimensions} dimensions", embeddingResponse.Embedding.Length);

        return new Embedding<float>(embeddingResponse.Embedding);
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

    private class OllamaEmbeddingRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; init; }

        [JsonPropertyName("prompt")]
        public required string Prompt { get; init; }
    }

    private class OllamaEmbeddingResponse
    {
        [JsonPropertyName("embedding")]
        public required float[] Embedding { get; init; }
    }
}

