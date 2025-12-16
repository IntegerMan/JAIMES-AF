namespace MattEland.Jaimes.ServiceDefinitions.Services;

public class JaimesChatOptions
{
    /// <summary>
    /// The provider to use: Ollama, OpenAI, or AzureOpenAI.
    /// </summary>
    public required ProviderType Provider { get; init; }

    /// <summary>
    /// Endpoint URL for the chat service.
    /// Required for Azure, optional for OpenAI (defaults to https://api.openai.com/v1).
    /// For Ollama, this is the Ollama server endpoint.
    /// </summary>
    public string? Endpoint { get; init; }

    /// <summary>
    /// API key for authentication.
    /// For Azure: if empty or "Identity", Azure Managed Identity will be used instead.
    /// Required for OpenAI when using ApiKey authentication.
    /// Optional for Ollama (typically not needed for local instances).
    /// </summary>
    public string? ApiKey { get; init; }

    /// <summary>
    /// Model name or deployment name.
    /// For Azure: deployment name (required).
    /// For OpenAI: model name (e.g., "gpt-4o-mini") (required).
    /// For Ollama: model name (e.g., "gemma3") (optional).
    /// </summary>
    public string? Name { get; init; }
}