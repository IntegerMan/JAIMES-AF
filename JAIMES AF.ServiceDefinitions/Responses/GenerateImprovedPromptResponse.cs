namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Response containing an AI-generated improved prompt.
/// </summary>
public class GenerateImprovedPromptResponse
{
    /// <summary>
    /// The AI-generated improved prompt.
    /// </summary>
    public string ImprovedPrompt { get; set; } = string.Empty;

    /// <summary>
    /// Whether the prompt generation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if generation failed.
    /// </summary>
    public string? Error { get; set; }
}
