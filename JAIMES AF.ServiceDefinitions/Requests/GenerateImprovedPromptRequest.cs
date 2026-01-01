namespace MattEland.Jaimes.ServiceDefinitions.Requests;

/// <summary>
/// Request to generate an improved prompt based on collected insights.
/// </summary>
public class GenerateImprovedPromptRequest
{
    /// <summary>
    /// The current/base prompt to improve upon.
    /// </summary>
    public string CurrentPrompt { get; set; } = string.Empty;

    /// <summary>
    /// Optional user-provided feedback or requests for specific improvements.
    /// </summary>
    public string? UserFeedback { get; set; }

    /// <summary>
    /// AI-generated insights from feedback data analysis.
    /// </summary>
    public string? FeedbackInsights { get; set; }

    /// <summary>
    /// AI-generated insights from metrics data analysis.
    /// </summary>
    public string? MetricsInsights { get; set; }

    /// <summary>
    /// AI-generated insights from sentiment data analysis.
    /// </summary>
    public string? SentimentInsights { get; set; }
}
