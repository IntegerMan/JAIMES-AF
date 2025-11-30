namespace MattEland.Jaimes.ServiceDefaults;

/// <summary>
/// Provider types for embedding and text generation model services.
/// </summary>
public enum ProviderType
{
    /// <summary>
    /// Ollama provider (local or remote Ollama server).
    /// </summary>
    Ollama,

    /// <summary>
    /// OpenAI provider (OpenAI API).
    /// </summary>
    OpenAI,

    /// <summary>
    /// Azure OpenAI provider (Azure OpenAI service).
    /// </summary>
    AzureOpenAI
}

