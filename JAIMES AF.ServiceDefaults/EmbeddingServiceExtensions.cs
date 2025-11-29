using System.ClientModel;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using Microsoft.Agents.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OllamaSharp;

namespace MattEland.Jaimes.ServiceDefaults;

/// <summary>
/// Extension methods for configuring embedding services in dependency injection containers.
/// </summary>
public static class EmbeddingServiceExtensions
{
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
            if (string.IsNullOrWhiteSpace(options.Endpoint) && !string.IsNullOrWhiteSpace(defaultOllamaEndpoint))
            {
                options.Endpoint = defaultOllamaEndpoint;
            }

            // Use default Ollama model if not configured
            if (string.IsNullOrWhiteSpace(options.Name) && !string.IsNullOrWhiteSpace(defaultOllamaModel))
            {
                options.Name = defaultOllamaModel;
            }
        }

        // Register options
        services.AddSingleton(options);

        // Register HttpClient if not already registered (needed for Azure OpenAI and OpenAI)
        services.AddHttpClient();

        // Register the appropriate embedding generator based on provider
        // Uses Microsoft.Extensions.AI's IEmbeddingGenerator<string, Embedding<float>> interface
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

                    return client.GetEmbeddingClient(opts.Name).AsIEmbeddingGenerator();
                });
                break;

            case ProviderType.OpenAI:
                services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
                {
                    EmbeddingModelOptions opts = sp.GetRequiredService<EmbeddingModelOptions>();
                    ILogger logger = sp.GetRequiredService<ILogger<IEmbeddingGenerator<string, Embedding<float>>>>();
                    const string DefaultOpenAIEndpoint = "https://api.openai.com/v1";

                    if (string.IsNullOrWhiteSpace(opts.Name))
                    {
                        throw new InvalidOperationException("OpenAI model name is not configured. Set EmbeddingModel:Name.");
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
                        throw new InvalidOperationException("OpenAI API key is not configured. Set EmbeddingModel:Key.");
                    }

                    string endpoint = opts.Endpoint ?? DefaultOpenAIEndpoint;

                    logger.LogDebug("Creating OpenAI embedding generator with model {Model} at {Endpoint}",
                        opts.Name, endpoint);

                    // OpenAI uses the same Azure OpenAI client but with OpenAI endpoint
                    AzureOpenAIClient client = new(
                        new Uri(endpoint),
                        new ApiKeyCredential(opts.Key!));

                    return client.GetEmbeddingClient(opts.Name).AsIEmbeddingGenerator();
                });
                break;

            case ProviderType.Ollama:
            default:
                services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
                {
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

                    logger.LogDebug("Creating Ollama embedding generator with model {Model} at {Endpoint}",
                        opts.Name, opts.Endpoint);

                    // OllamaApiClient from OllamaSharp already implements IEmbeddingGenerator<string, Embedding<float>>
                    Uri ollamaUri = new(opts.Endpoint);
                    OllamaApiClient client = new(ollamaUri, opts.Name);
                    return client;
                });
                break;
        }

        return services;
    }
}

