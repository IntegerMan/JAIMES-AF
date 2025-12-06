namespace MattEland.Jaimes.ServiceDefaults;

/// <summary>
/// Configuration options for text generation model providers.
/// </summary>
public class TextGenerationModelOptions
{
    /// <summary>
    /// The provider to use: Ollama, OpenAI, or AzureOpenAI.
    /// Defaults to Ollama.
    /// </summary>
    public ProviderType Provider { get; set; } = ProviderType.Ollama;

    /// <summary>
    /// Authentication method.
    /// None: No authentication (e.g., local Ollama).
    /// ApiKey: API key authentication (Azure OpenAI, OpenAI, optionally Ollama).
    /// Identity: Azure Managed Identity (Azure only).
    /// Defaults to ApiKey, but will be set to None for Ollama provider.
    /// </summary>
    public AuthenticationType Auth { get; set; } = AuthenticationType.ApiKey;

    /// <summary>
    /// API key for authentication (required for Azure and OpenAI).
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// Endpoint URL for the text generation service.
    /// Required for Azure, optional for OpenAI (defaults to https://api.openai.com/v1).
    /// For Ollama, this is the Ollama server endpoint.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Model name or deployment name.
    /// For Azure: deployment name.
    /// For OpenAI: model name (e.g., "gpt-4o-mini").
    /// For Ollama: model name (e.g., "gemma3").
    /// </summary>
    public string? Name { get; set; }
}