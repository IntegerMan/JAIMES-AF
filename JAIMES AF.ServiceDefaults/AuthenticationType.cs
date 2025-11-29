namespace MattEland.Jaimes.ServiceDefaults;

/// <summary>
/// Authentication types for embedding and text generation model providers.
/// </summary>
public enum AuthenticationType
{
    /// <summary>
    /// No authentication required (e.g., local Ollama).
    /// </summary>
    None,

    /// <summary>
    /// API key authentication (used for Azure OpenAI, OpenAI, and optionally Ollama).
    /// </summary>
    ApiKey,

    /// <summary>
    /// Azure Managed Identity authentication (Azure only).
    /// </summary>
    Identity
}

