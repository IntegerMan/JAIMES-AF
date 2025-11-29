using System.ClientModel;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using Microsoft.Agents.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MattEland.Jaimes.ServiceDefaults;

/// <summary>
/// Extension methods for configuring text generation services in dependency injection containers.
/// </summary>
public static class TextGenerationServiceExtensions
{
    /// <summary>
    /// Configures and registers an IChatClient based on configuration.
    /// Supports Ollama (default), Azure OpenAI, and OpenAI providers.
    /// Uses Microsoft.Extensions.AI's IChatClient interface.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration instance.</param>
    /// <param name="sectionName">The configuration section name (defaults to "TextGenerationModel").</param>
    /// <param name="defaultOllamaEndpoint">Default Ollama endpoint if not configured (for Aspire integration).</param>
    /// <param name="defaultOllamaModel">Default Ollama model name if not configured (for Aspire integration).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddChatClient(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "TextGenerationModel",
        string? defaultOllamaEndpoint = null,
        string? defaultOllamaModel = null)
    {
        // Bind configuration
        TextGenerationModelOptions options = configuration.GetSection(sectionName).Get<TextGenerationModelOptions>() ?? new TextGenerationModelOptions();

        // Parse Provider enum from string if needed (configuration binding may provide string)
        string? providerConfigValue = configuration[$"{sectionName}:Provider"];
        if (!string.IsNullOrWhiteSpace(providerConfigValue))
        {
            // Handle legacy "Azure" value and map to AzureOpenAI
            if (string.Equals(providerConfigValue, "Azure", StringComparison.OrdinalIgnoreCase))
            {
                options.Provider = ProviderType.AzureOpenAI;
            }
            else if (Enum.TryParse<ProviderType>(providerConfigValue, ignoreCase: true, out ProviderType providerType))
            {
                options.Provider = providerType;
            }
        }

        // Parse Auth enum from string if needed (configuration binding may provide string)
        // Check if Auth is still at default value (0 = None) and try to parse from config
        string? authConfigValue = configuration[$"{sectionName}:Auth"];
        if (!string.IsNullOrWhiteSpace(authConfigValue))
        {
            if (Enum.TryParse<AuthenticationType>(authConfigValue, ignoreCase: true, out AuthenticationType authType))
            {
                options.Auth = authType;
            }
        }

        // If provider is Ollama, use defaults from Aspire if available
        if (options.Provider == ProviderType.Ollama)
        {
            // Default to None auth for Ollama (local instances don't need auth)
            if (options.Auth == default)
            {
                options.Auth = AuthenticationType.None;
            }

            // Use default Ollama endpoint if not configured
            if (string.IsNullOrWhiteSpace(options.Endpoint))
            {
                if (!string.IsNullOrWhiteSpace(defaultOllamaEndpoint))
                {
                    options.Endpoint = defaultOllamaEndpoint;
                }
                else
                {
                    // Default to localhost if no endpoint is configured
                    options.Endpoint = "http://localhost:11434";
                }
            }

            // Use default Ollama model if not configured
            if (string.IsNullOrWhiteSpace(options.Name))
            {
                if (!string.IsNullOrWhiteSpace(defaultOllamaModel))
                {
                    options.Name = defaultOllamaModel;
                }
                else
                {
                    // Default model name if none specified
                    options.Name = "gemma3";
                }
            }
        }

        // Register options
        services.AddSingleton(options);

        // Register HttpClient if not already registered (needed for Ollama)
        services.AddHttpClient();

        // Register the appropriate chat client based on provider
        switch (options.Provider)
        {
            case ProviderType.AzureOpenAI:
                services.AddSingleton<IChatClient>(sp =>
                {
                    TextGenerationModelOptions opts = sp.GetRequiredService<TextGenerationModelOptions>();
                    ILogger logger = sp.GetRequiredService<ILogger<IChatClient>>();

                    if (string.IsNullOrWhiteSpace(opts.Endpoint))
                    {
                        throw new InvalidOperationException("Azure OpenAI endpoint is not configured. Set TextGenerationModel:Endpoint.");
                    }

                    if (string.IsNullOrWhiteSpace(opts.Name))
                    {
                        throw new InvalidOperationException("Azure OpenAI deployment name is not configured. Set TextGenerationModel:Name.");
                    }

                    if (opts.Auth == AuthenticationType.ApiKey && string.IsNullOrWhiteSpace(opts.Key))
                    {
                        throw new InvalidOperationException("Azure OpenAI API key is not configured. Set TextGenerationModel:Key.");
                    }

                    logger.LogDebug("Creating Azure OpenAI chat client with deployment {Deployment} at {Endpoint} with auth type {AuthType}",
                        opts.Name, opts.Endpoint, opts.Auth);

                    AzureOpenAIClient client;
                    if (opts.Auth == AuthenticationType.ApiKey)
                    {
                        client = new AzureOpenAIClient(
                            new Uri(opts.Endpoint),
                            new ApiKeyCredential(opts.Key!));
                    }
                    else if (opts.Auth == AuthenticationType.Identity)
                    {
                        DefaultAzureCredential credential = new();
                        client = new AzureOpenAIClient(
                            new Uri(opts.Endpoint),
                            credential);
                    }
                    else
                    {
                        throw new InvalidOperationException($"Authentication type '{opts.Auth}' is not supported for Azure OpenAI. Use ApiKey or Identity.");
                    }

                    return client.GetChatClient(opts.Name).AsIChatClient();
                });
                break;

            case ProviderType.OpenAI:
                services.AddSingleton<IChatClient>(sp =>
                {
                    TextGenerationModelOptions opts = sp.GetRequiredService<TextGenerationModelOptions>();
                    ILogger logger = sp.GetRequiredService<ILogger<IChatClient>>();
                    const string DefaultOpenAIEndpoint = "https://api.openai.com/v1";

                    if (string.IsNullOrWhiteSpace(opts.Name))
                    {
                        throw new InvalidOperationException("OpenAI model name is not configured. Set TextGenerationModel:Name.");
                    }

                    if (opts.Auth == AuthenticationType.None)
                    {
                        throw new InvalidOperationException("OpenAI does not support None authentication. Use ApiKey.");
                    }

                    if (opts.Auth == AuthenticationType.Identity)
                    {
                        throw new InvalidOperationException("Identity authentication is only supported for Azure OpenAI, not OpenAI.");
                    }

                    if (opts.Auth == AuthenticationType.ApiKey && string.IsNullOrWhiteSpace(opts.Key))
                    {
                        throw new InvalidOperationException("OpenAI API key is not configured. Set TextGenerationModel:Key.");
                    }

                    string endpoint = opts.Endpoint ?? DefaultOpenAIEndpoint;

                    logger.LogDebug("Creating OpenAI chat client with model {Model} at {Endpoint} with auth type {AuthType}",
                        opts.Name, endpoint, opts.Auth);

                    AzureOpenAIClient client = new(
                        new Uri(endpoint),
                        new ApiKeyCredential(opts.Key!));

                    return client.GetChatClient(opts.Name).AsIChatClient();
                });
                break;

            case ProviderType.Ollama:
            default:
                services.AddHttpClient<OllamaChatClientWrapper>();
                services.AddSingleton<IChatClient>(sp =>
                {
                    IHttpClientFactory httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                    HttpClient httpClient = httpClientFactory.CreateClient(nameof(OllamaChatClientWrapper));
                    TextGenerationModelOptions opts = sp.GetRequiredService<TextGenerationModelOptions>();
                    ILogger<OllamaChatClientWrapper> logger = sp.GetRequiredService<ILogger<OllamaChatClientWrapper>>();

                    if (string.IsNullOrWhiteSpace(opts.Endpoint))
                    {
                        throw new InvalidOperationException("Ollama endpoint is not configured. Set TextGenerationModel:Endpoint.");
                    }

                    if (string.IsNullOrWhiteSpace(opts.Name))
                    {
                        throw new InvalidOperationException("Ollama model name is not configured. Set TextGenerationModel:Name.");
                    }

                    logger.LogDebug("Creating Ollama chat client with model {Model} at {Endpoint}",
                        opts.Name, opts.Endpoint);

                    return new OllamaChatClientWrapper(httpClient, opts.Endpoint, opts.Name, logger);
                });
                break;
        }

        return services;
    }

    /// <summary>
    /// Simple wrapper that implements IChatClient for Ollama using HTTP API calls.
    /// </summary>
    private sealed class OllamaChatClientWrapper : IChatClient, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _endpoint;
        private readonly string _model;
        private readonly ILogger _logger;

        public OllamaChatClientWrapper(HttpClient httpClient, string endpoint, string model, ILogger logger)
        {
            _httpClient = httpClient;
            _endpoint = endpoint.TrimEnd('/');
            _model = model;
            _logger = logger;
        }

        public void Dispose()
        {
            // HttpClient is managed by DI, so we don't dispose it here
        }

        public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            // Convert ChatMessage collection to Ollama chat format
            List<OllamaChatMessage> ollamaMessages = [];
            foreach (ChatMessage message in messages)
            {
                string role;
                if (message.Role == ChatRole.System)
                {
                    role = "system";
                }
                else if (message.Role == ChatRole.User)
                {
                    role = "user";
                }
                else if (message.Role == ChatRole.Assistant)
                {
                    role = "assistant";
                }
                else
                {
                    role = "user";
                }

                ollamaMessages.Add(new OllamaChatMessage
                {
                    Role = role,
                    Content = message.Text ?? string.Empty
                });
            }

            string requestUrl = $"{_endpoint}/api/chat";

            OllamaChatRequest request = new()
            {
                Model = _model,
                Messages = ollamaMessages,
                Stream = false
            };

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(requestUrl, request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to generate text. Status: {StatusCode}, Response: {Response}",
                    response.StatusCode, errorContent);
                response.EnsureSuccessStatusCode();
            }

            OllamaChatResponse? chatResponse = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(
                cancellationToken: cancellationToken);

            if (chatResponse?.Message == null || string.IsNullOrWhiteSpace(chatResponse.Message.Content))
            {
                throw new InvalidOperationException("Received empty response from Ollama");
            }

            // Create a ChatResponse from the Ollama response
            ChatMessage responseMessage = new(ChatRole.Assistant, chatResponse.Message.Content);
            ChatResponse result = new();
            result.Messages.Add(responseMessage);

            return result;
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // For streaming, we'd need to handle SSE (Server-Sent Events) from Ollama
            // This is a simplified implementation that falls back to non-streaming
            await Task.CompletedTask;
            yield break;
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
        {
            return null;
        }

        private record OllamaChatRequest
        {
            [JsonPropertyName("model")]
            public required string Model { get; init; }

            [JsonPropertyName("messages")]
            public required List<OllamaChatMessage> Messages { get; init; }

            [JsonPropertyName("stream")]
            public bool Stream { get; init; }
        }

        private record OllamaChatMessage
        {
            [JsonPropertyName("role")]
            public required string Role { get; init; }

            [JsonPropertyName("content")]
            public required string Content { get; init; }
        }

        private record OllamaChatResponse
        {
            [JsonPropertyName("model")]
            public string? Model { get; init; }

            [JsonPropertyName("message")]
            public OllamaChatMessage? Message { get; init; }

            [JsonPropertyName("usage")]
            public OllamaUsage? Usage { get; init; }
        }

        private record OllamaUsage
        {
            [JsonPropertyName("prompt_tokens")]
            public int? PromptTokens { get; init; }

            [JsonPropertyName("completion_tokens")]
            public int? CompletionTokens { get; init; }

            [JsonPropertyName("total_tokens")]
            public int? TotalTokens { get; init; }
        }
    }
}

