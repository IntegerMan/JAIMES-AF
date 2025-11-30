using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MattEland.Jaimes.ServiceDefaults;

/// <summary>
/// Extension methods for configuring embedding services in dependency injection containers.
/// </summary>
public static class EmbeddingServiceExtensions
{
    /// <summary>
    /// Parses an Ollama connection string from Aspire to extract endpoint and model name.
    /// </summary>
    /// <param name="connectionString">The connection string from Aspire (e.g., "Endpoint=http://localhost:11434;Model=nomic-embed-text").</param>
    /// <returns>A tuple containing the endpoint and model name, or (null, null) if parsing fails.</returns>
    public static (string? Endpoint, string? Model) ParseOllamaConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return (null, null);
        }

        // Check if it's a semicolon-delimited connection string with key=value pairs
        if (connectionString.Contains("Endpoint=", StringComparison.OrdinalIgnoreCase))
        {
            string[] parts = connectionString.Split(';');
            string? endpoint = parts.FirstOrDefault(p => p.StartsWith("Endpoint=", StringComparison.OrdinalIgnoreCase))
                ?.Substring("Endpoint=".Length);
            string? model = parts.FirstOrDefault(p => p.StartsWith("Model=", StringComparison.OrdinalIgnoreCase))
                ?.Substring("Model=".Length);
            
            return (endpoint?.TrimEnd('/'), model);
        }
        
        // Otherwise assume it's just a plain endpoint URL
        return (connectionString.TrimEnd('/'), null);
    }

    /// <summary>
    /// Configures and registers an embedding generator based on configuration.
    /// Supports Ollama (default), Azure OpenAI, and OpenAI providers.
    /// Uses Microsoft.Extensions.AI's IEmbeddingGenerator&lt;string, Embedding&lt;float&gt;&gt; interface.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration instance.</param>
    /// <param name="sectionName">The configuration section name (defaults to "EmbeddingModel").</param>
    /// <param name="defaultOllamaEndpoint">Default Ollama endpoint if not configured (for Aspire integration).</param>
    /// <param name="defaultOllamaModel">Default Ollama model name if not configured (for Aspire integration).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEmbeddingGenerator(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "EmbeddingModel",
        string? defaultOllamaEndpoint = null,
        string? defaultOllamaModel = null)
    {
        // Bind configuration
        EmbeddingModelOptions options = configuration.GetSection(sectionName).Get<EmbeddingModelOptions>() ?? new EmbeddingModelOptions();

        // Normalize Provider/Auth from string config values
        options.Provider = AiModelConfiguration.NormalizeProvider(configuration, sectionName, options.Provider);
        options.Auth = AiModelConfiguration.NormalizeAuth(configuration, sectionName, options.Auth);

        // If provider is Ollama, apply sensible defaults
        if (options.Provider == ProviderType.Ollama)
        {
            (AuthenticationType resolvedAuth, string endpoint, string name) = AiModelConfiguration.ApplyOllamaDefaults(
                options.Auth,
                options.Endpoint,
                options.Name,
                defaultOllamaEndpoint,
                defaultOllamaModel,
                fallbackEndpoint: "http://localhost:11434",
                fallbackModel: "nomic-embed-text");

            options.Auth = resolvedAuth;
            options.Endpoint = endpoint;
            options.Name = name;
        }

        // Register options
        services.AddSingleton(options);

        // Register HttpClient if not already registered (needed for Azure OpenAI/OpenAI and Ollama)
        services.AddHttpClient();

        // Register the appropriate embedding generator based on provider
        switch (options.Provider)
        {
            case ProviderType.AzureOpenAI:
                services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
                {
                    EmbeddingModelOptions opts = sp.GetRequiredService<EmbeddingModelOptions>();
                    ILogger logger = sp.GetRequiredService<ILogger<IEmbeddingGenerator<string, Embedding<float>>>>();

                    if (string.IsNullOrWhiteSpace(opts.Endpoint))
                    {
                        throw new InvalidOperationException("Azure OpenAI endpoint is not configured. Set EmbeddingModel:Endpoint.");
                    }

                    if (string.IsNullOrWhiteSpace(opts.Name))
                    {
                        throw new InvalidOperationException("Azure OpenAI deployment name is not configured. Set EmbeddingModel:Name.");
                    }

                    if (opts.Auth == AuthenticationType.ApiKey && string.IsNullOrWhiteSpace(opts.Key))
                    {
                        throw new InvalidOperationException("Azure OpenAI API key is not configured. Set EmbeddingModel:Key.");
                    }

                    logger.LogDebug("Creating Azure OpenAI embedding generator with deployment {Deployment} at {Endpoint} with auth type {AuthType}",
                        opts.Name, opts.Endpoint, opts.Auth);

                    AzureOpenAIClient client = AiModelConfiguration.CreateAzureOpenAIClient(opts.Endpoint!, opts.Auth, opts.Key);
                    return client.GetEmbeddingClient(opts.Name).AsIEmbeddingGenerator();
                });
                break;

            case ProviderType.OpenAI:
                services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
                {
                    EmbeddingModelOptions opts = sp.GetRequiredService<EmbeddingModelOptions>();
                    ILogger logger = sp.GetRequiredService<ILogger<IEmbeddingGenerator<string, Embedding<float>>>>();

                    if (string.IsNullOrWhiteSpace(opts.Name))
                    {
                        throw new InvalidOperationException("OpenAI model name is not configured. Set EmbeddingModel:Name.");
                    }

                    if (opts.Auth != AuthenticationType.ApiKey)
                    {
                        throw new InvalidOperationException("OpenAI requires ApiKey authentication.");
                    }

                    if (string.IsNullOrWhiteSpace(opts.Key))
                    {
                        throw new InvalidOperationException("OpenAI API key is not configured. Set EmbeddingModel:Key.");
                    }

                    logger.LogDebug("Creating OpenAI-compatible embedding generator for model {Model} (Endpoint: {Endpoint})",
                        opts.Name, opts.Endpoint ?? "https://api.openai.com/v1");

                    AzureOpenAIClient client = AiModelConfiguration.CreateOpenAICompatibleClient(opts.Endpoint, opts.Key!);
                    return client.GetEmbeddingClient(opts.Name).AsIEmbeddingGenerator();
                });
                break;

            case ProviderType.Ollama:
            default:
                services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
                {
                    IHttpClientFactory httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                    HttpClient httpClient = httpClientFactory.CreateClient();
                    EmbeddingModelOptions opts = sp.GetRequiredService<EmbeddingModelOptions>();
                    ILogger logger = sp.GetRequiredService<ILogger<IEmbeddingGenerator<string, Embedding<float>>>>();

                    if (string.IsNullOrWhiteSpace(opts.Endpoint))
                    {
                        throw new InvalidOperationException("Ollama endpoint is not configured. Set EmbeddingModel:Endpoint.");
                    }

                    if (string.IsNullOrWhiteSpace(opts.Name))
                    {
                        throw new InvalidOperationException("Ollama model name is not configured. Set EmbeddingModel:Name.");
                    }

                    logger.LogInformation("Creating Ollama embedding generator with model {Model} at {Endpoint}. " +
                        "Ensure the model is available in Ollama (run 'ollama pull {ModelId}' if needed).",
                        opts.Name, opts.Endpoint, opts.Name);

                    return new OllamaEmbeddingGeneratorWrapper(httpClient, opts.Endpoint, opts.Name, logger);
                });
                break;
        }

        return services;
    }

    /// <summary>
    /// Wrapper that implements IEmbeddingGenerator for Ollama using HTTP API calls with better error messages.
    /// </summary>
    private sealed class OllamaEmbeddingGeneratorWrapper : IEmbeddingGenerator<string, Embedding<float>>, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _endpoint;
        private readonly string _modelName;
        private readonly ILogger _logger;

        public OllamaEmbeddingGeneratorWrapper(HttpClient httpClient, string endpoint, string modelName, ILogger logger)
        {
            _httpClient = httpClient;
            _endpoint = endpoint;
            _modelName = modelName;
            _logger = logger;
        }

        public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> inputs, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
        {
            List<string> inputList = inputs.ToList();
            if (inputList.Count == 0)
            {
                return new GeneratedEmbeddings<Embedding<float>>(Array.Empty<Embedding<float>>());
            }

            _logger.LogDebug("Generating {Count} embedding(s) using Ollama model {Model} at {Endpoint}",
                inputList.Count, _modelName, _endpoint);

            List<Embedding<float>> embeddings = [];

            // Ollama processes embeddings one at a time
            foreach (string input in inputList)
            {
                string requestUrl = $"{_endpoint.TrimEnd('/')}/api/embeddings";

                OllamaEmbeddingRequest request = new()
                {
                    Model = _modelName,
                    Prompt = input
                };

                try
                {
                    HttpResponseMessage response = await _httpClient.PostAsJsonAsync(requestUrl, request, cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                        
                        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            _logger.LogError(
                                "Ollama returned 404 (Not Found) when generating embeddings. " +
                                "This usually means the model '{Model}' is not available in Ollama at {Endpoint}. " +
                                "Response: {Response}. To fix this, run: ollama pull {ModelId}",
                                _modelName, _endpoint, errorContent, _modelName);
							
                            throw new InvalidOperationException(
                                $"Ollama model '{_modelName}' not found at {_endpoint}. " +
                                $"Ensure the model is available by running: ollama pull {_modelName}. " +
                                $"Ollama response: {errorContent}");
                        }
						
                        _logger.LogError("Failed to generate embedding. Status: {StatusCode}, Response: {Response}",
                            response.StatusCode, errorContent);
                        response.EnsureSuccessStatusCode();
                    }

                    OllamaEmbeddingResponse? embeddingResponse = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(
                        cancellationToken: cancellationToken);

                    if (embeddingResponse?.Embedding == null || embeddingResponse.Embedding.Length == 0)
                    {
                        throw new InvalidOperationException("Received empty embedding from Ollama");
                    }

                    ReadOnlyMemory<float> embeddingVector = embeddingResponse.Embedding;
                    Embedding<float> embedding = new(embeddingVector);
                    _logger.LogDebug("Generated embedding with {Dimensions} dimensions", embeddingVector.Length);
                    embeddings.Add(embedding);
                }
                catch (HttpRequestException ex) when (ex.Message.Contains("404") || ex.Message.Contains("Not Found"))
                {
                    _logger.LogError(ex, 
                        "Ollama returned 404 (Not Found) when generating embeddings. " +
                        "This usually means the model '{Model}' is not available in Ollama at {Endpoint}. " +
                        "To fix this, run: ollama pull {ModelId}",
                        _modelName, _endpoint, _modelName);
					
                    throw new InvalidOperationException(
                        $"Ollama model '{_modelName}' not found at {_endpoint}. " +
                        $"Ensure the model is available by running: ollama pull {_modelName}. " +
                        $"Original error: {ex.Message}", ex);
                }
            }

            return new GeneratedEmbeddings<Embedding<float>>(embeddings);
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
        {
            return null;
        }

        public void Dispose()
        {
            // HttpClient is managed by DI, so we don't dispose it here
        }

        private record OllamaEmbeddingRequest
        {
            [JsonPropertyName("model")]
            public required string Model { get; init; }

            [JsonPropertyName("prompt")]
            public required string Prompt { get; init; }
        }

        private record OllamaEmbeddingResponse
        {
            [JsonPropertyName("embedding")]
            public required float[] Embedding { get; init; }
        }
    }
}

