using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using MattEland.Jaimes.Workers.DocumentChunking.Configuration;

namespace MattEland.Jaimes.Workers.DocumentChunking.Services;

public class OllamaEmbeddingService(
    HttpClient httpClient,
    OllamaEmbeddingOptions options,
    ILogger<OllamaEmbeddingService> logger) : IOllamaEmbeddingService
{
    // Semaphore to ensure only one embedding request is processed at a time globally
    // This prevents overwhelming Ollama with concurrent requests from SemanticChunker.NET
    // which may call GenerateEmbeddingAsync multiple times concurrently during chunking
    private static readonly SemaphoreSlim _embeddingSemaphore = new(1, 1);
    private static int _activeRequests = 0;

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text cannot be null or empty", nameof(text));
        }

        // Acquire semaphore to ensure only one embedding request at a time
        int currentActive = Interlocked.Increment(ref _activeRequests);
        logger.LogDebug("Embedding request queued. Active requests: {ActiveRequests}", currentActive);
        
        await _embeddingSemaphore.WaitAsync(cancellationToken);
        try
        {
            Interlocked.Decrement(ref _activeRequests);
            logger.LogDebug("Processing embedding request. Active requests: {ActiveRequests}", _activeRequests);
            
            string requestUrl = $"{options.Endpoint}/api/embeddings";
            
            OllamaEmbeddingRequest request = new()
            {
                Model = options.Model,
                Prompt = text
            };

            logger.LogDebug("Generating embedding for text (length: {Length}) using model {Model}", text.Length, options.Model);

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

            return embeddingResponse.Embedding;
        }
        finally
        {
            _embeddingSemaphore.Release();
        }
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

