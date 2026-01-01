using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Azure.AI.OpenAI;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.ServiceDefaults.Common;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAI;

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
        TextGenerationModelOptions options = configuration.GetSection(sectionName).Get<TextGenerationModelOptions>() ??
                                             new TextGenerationModelOptions();

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
                "http://localhost:11434",
                "gemma3");

            options.Auth = resolvedAuth;
            options.Endpoint = endpoint;
            options.Name = name;
        }

        // Register options
        services.AddSingleton(options);

        // Register HttpClient if not already registered (needed for Ollama)
        services.AddHttpClient();

        // Register the appropriate chat client based on provider
        switch (options.Provider)
        {
            case ProviderType.AzureOpenAi:
                services.AddSingleton<IChatClient>(sp =>
                {
                    TextGenerationModelOptions opts = sp.GetRequiredService<TextGenerationModelOptions>();
                    ILogger logger = sp.GetRequiredService<ILogger<IChatClient>>();

                    if (string.IsNullOrWhiteSpace(opts.Endpoint))
                        throw new InvalidOperationException(
                            "Azure OpenAI endpoint is not configured. Set TextGenerationModel:Endpoint.");

                    if (string.IsNullOrWhiteSpace(opts.Name))
                        throw new InvalidOperationException(
                            "Azure OpenAI deployment name is not configured. Set TextGenerationModel:Name.");

                    if (opts.Auth == AuthenticationType.ApiKey && string.IsNullOrWhiteSpace(opts.Key))
                        throw new InvalidOperationException(
                            "Azure OpenAI API key is not configured. Set TextGenerationModel:Key.");

                    logger.LogDebug(
                        "Creating Azure OpenAI chat client with deployment {Deployment} at {Endpoint} with auth type {AuthType}",
                        opts.Name,
                        opts.Endpoint,
                        opts.Auth);

                    AzureOpenAIClient client =
                        AiModelConfiguration.CreateAzureOpenAiClient(opts.Endpoint!, opts.Auth, opts.Key);
                    return client.GetChatClient(opts.Name).AsIChatClient();
                });
                break;

            case ProviderType.OpenAi:
                services.AddSingleton<IChatClient>(sp =>
                {
                    TextGenerationModelOptions opts = sp.GetRequiredService<TextGenerationModelOptions>();
                    ILogger logger = sp.GetRequiredService<ILogger<IChatClient>>();

                    if (string.IsNullOrWhiteSpace(opts.Name))
                        throw new InvalidOperationException(
                            "OpenAI model name is not configured. Set TextGenerationModel:Name.");

                    if (opts.Auth != AuthenticationType.ApiKey)
                        throw new InvalidOperationException("OpenAI requires ApiKey authentication.");

                    if (string.IsNullOrWhiteSpace(opts.Key))
                        throw new InvalidOperationException(
                            "OpenAI API key is not configured. Set TextGenerationModel:Key.");

                    logger.LogDebug("Creating OpenAI chat client with model {Model} at {Endpoint}",
                        opts.Name,
                        opts.Endpoint ?? "https://api.openai.com/v1");

                    OpenAIClient client = AiModelConfiguration.CreateOpenAiClient(opts.Endpoint, opts.Key!);
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
                        throw new InvalidOperationException(
                            "Ollama endpoint is not configured. Set TextGenerationModel:Endpoint.");

                    if (string.IsNullOrWhiteSpace(opts.Name))
                        throw new InvalidOperationException(
                            "Ollama model name is not configured. Set TextGenerationModel:Name.");

                    logger.LogDebug("Creating Ollama chat client with model {Model} at {Endpoint}",
                        opts.Name,
                        opts.Endpoint);

                    return new OllamaChatClientWrapper(httpClient, opts.Endpoint, opts.Name, logger);
                });
                break;
        }

        return services;
    }

    /// <summary>
    /// Simple wrapper that implements IChatClient for Ollama using HTTP API calls.
    /// </summary>
    private sealed class OllamaChatClientWrapper(HttpClient httpClient, string endpoint, string model, ILogger logger)
        : IChatClient, IDisposable
    {
        private readonly string _endpoint = endpoint.TrimEnd('/');

        public void Dispose()
        {
            // HttpClient is managed by DI, so we don't dispose it here
        }

        public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            logger.LogInformation("GetResponseAsync called (Non-streaming) for model {Model}", model);

            // Convert ChatMessage collection to Ollama chat format
            List<OllamaChatMessage> ollamaMessages = [];
            foreach (ChatMessage message in messages)
            {
                string role;
                if (message.Role == ChatRole.System)
                    role = "system";
                else if (message.Role == ChatRole.User)
                    role = "user";
                else if (message.Role == ChatRole.Assistant)
                    role = "assistant";
                else
                    role = "user";

                ollamaMessages.Add(new OllamaChatMessage
                {
                    Role = role,
                    Content = message.Text ?? string.Empty
                });
            }

            string requestUrl = $"{_endpoint}/api/chat";

            OllamaChatRequest request = new()
            {
                Model = model,
                Messages = ollamaMessages,
                Stream = false
            };

            HttpResponseMessage response = await httpClient.PostAsJsonAsync(requestUrl, request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError("Failed to generate text. Status: {StatusCode}, Response: {Response}",
                    response.StatusCode,
                    errorContent);
                response.EnsureSuccessStatusCode();
            }

            OllamaChatResponse? chatResponse = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(
                cancellationToken);

            if (chatResponse?.Message == null || string.IsNullOrWhiteSpace(chatResponse.Message.Content))
                throw new InvalidOperationException("Received empty response from Ollama");

            // Create a ChatResponse from the Ollama response
            ChatMessage responseMessage = new(ChatRole.Assistant, chatResponse.Message.Content);
            ChatResponse result = new();
            result.Messages.Add(responseMessage);

            return result;
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            logger.LogInformation("Starting streaming response for model {Model}", model);

            // Convert ChatMessage collection to Ollama chat format
            List<OllamaChatMessage> ollamaMessages = [];
            foreach (ChatMessage message in messages)
            {
                string role;
                if (message.Role == ChatRole.System)
                    role = "system";
                else if (message.Role == ChatRole.User)
                    role = "user";
                else if (message.Role == ChatRole.Assistant)
                    role = "assistant";
                else
                    role = "user";

                ollamaMessages.Add(new OllamaChatMessage
                {
                    Role = role,
                    Content = message.Text ?? string.Empty
                });
            }

            string requestUrl = $"{_endpoint}/api/chat";

            OllamaChatRequest request = new()
            {
                Model = model,
                Messages = ollamaMessages,
                Stream = true
            };

            // Send streaming request
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUrl)
            {
                Content = JsonContent.Create(request)
            };

            using HttpResponseMessage response = await httpClient.SendAsync(httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError("Failed to stream text. Status: {StatusCode}, Response: {Response}",
                    response.StatusCode,
                    errorContent);
                response.EnsureSuccessStatusCode();
            }

            // Read SSE stream
            using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using StreamReader reader = new(stream);

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                string? line = await reader.ReadLineAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(line)) continue;

                logger.LogInformation("Ollama SSE Line: {LineLength}", line.Length);

                OllamaChatResponse? chatResponse = null;
                try
                {
                    chatResponse = System.Text.Json.JsonSerializer.Deserialize<OllamaChatResponse>(line);
                }
                catch (System.Text.Json.JsonException)
                {
                    // Ignore malformed JSON chunks
                    continue;
                }

                if (chatResponse?.Message?.Content != null)
                {
                    yield return new ChatResponseUpdate
                    {
                        Role = ChatRole.Assistant,
                        Contents = {new TextContent(chatResponse.Message.Content)}
                    };
                }

                if (chatResponse?.Done == true)
                {
                    break;
                }
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
        {
            if (serviceType == typeof(ChatClientMetadata))
            {
                return new ChatClientMetadata(nameof(OllamaChatClientWrapper), new Uri(_endpoint), model);
            }

            return null;
        }

        private record OllamaChatRequest
        {
            [JsonPropertyName("model")] public required string Model { get; init; }

            [JsonPropertyName("messages")] public required List<OllamaChatMessage> Messages { get; init; }

            [JsonPropertyName("stream")] public bool Stream { get; init; }
        }

        private record OllamaChatMessage
        {
            [JsonPropertyName("role")] public required string Role { get; init; }

            [JsonPropertyName("content")] public required string Content { get; init; }
        }

        private record OllamaChatResponse
        {
            [JsonPropertyName("model")] public string? Model { get; init; }

            [JsonPropertyName("message")] public OllamaChatMessage? Message { get; init; }

            [JsonPropertyName("done")] public bool Done { get; init; }

            [JsonPropertyName("usage")] public OllamaUsage? Usage { get; init; }
        }

        private record OllamaUsage
        {
            [JsonPropertyName("prompt_tokens")] public int? PromptTokens { get; init; }

            [JsonPropertyName("completion_tokens")]
            public int? CompletionTokens { get; init; }

            [JsonPropertyName("total_tokens")] public int? TotalTokens { get; init; }
        }
    }
}