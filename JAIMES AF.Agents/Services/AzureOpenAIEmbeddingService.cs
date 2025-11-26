using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.Agents.Services;

public class AzureOpenAIEmbeddingService(
    HttpClient httpClient,
    JaimesChatOptions options,
    ILogger<AzureOpenAIEmbeddingService> logger) : IAzureOpenAIEmbeddingService
{
    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text cannot be null or empty", nameof(text));
        }

        logger.LogDebug("Generating embedding for text (length: {Length}) using deployment {Deployment}", 
            text.Length, options.EmbeddingDeployment);

        // Use Azure OpenAI REST API for embeddings
        string requestUrl = $"{options.Endpoint.TrimEnd('/')}/openai/deployments/{options.EmbeddingDeployment}/embeddings?api-version=2024-02-15-preview";
        
        AzureOpenAIEmbeddingRequest request = new()
        {
            Input = text
        };

        httpClient.DefaultRequestHeaders.Clear();
        httpClient.DefaultRequestHeaders.Add("api-key", options.ApiKey);

        HttpResponseMessage response = await httpClient.PostAsJsonAsync(requestUrl, request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("Failed to generate embedding. Status: {StatusCode}, Response: {Response}", 
                response.StatusCode, errorContent);
            response.EnsureSuccessStatusCode();
        }

        AzureOpenAIEmbeddingResponse? embeddingResponse = await response.Content.ReadFromJsonAsync<AzureOpenAIEmbeddingResponse>(
            cancellationToken: cancellationToken);

        if (embeddingResponse?.Data == null || embeddingResponse.Data.Count == 0 || embeddingResponse.Data[0].Embedding == null)
        {
            throw new InvalidOperationException("Received empty embedding from Azure OpenAI");
        }

        float[] embedding = embeddingResponse.Data[0].Embedding.ToArray();

        logger.LogDebug("Generated embedding with {Dimensions} dimensions", embedding.Length);

        return embedding;
    }

    private record AzureOpenAIEmbeddingRequest
    {
        [JsonPropertyName("input")]
        public required string Input { get; init; }
    }

    private record AzureOpenAIEmbeddingResponse
    {
        [JsonPropertyName("data")]
        public required List<AzureOpenAIEmbeddingData> Data { get; init; }
    }

    private record AzureOpenAIEmbeddingData
    {
        [JsonPropertyName("embedding")]
        public required float[] Embedding { get; init; }
    }
}

