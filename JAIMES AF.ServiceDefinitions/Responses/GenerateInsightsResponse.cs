namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Response containing AI-generated insights for a specific data type.
/// </summary>
public class GenerateInsightsResponse
{
    /// <summary>
    /// Type of insight that was generated: "feedback", "metrics", or "sentiment".
    /// </summary>
    public string InsightType { get; set; } = string.Empty;

    /// <summary>
    /// The AI-generated coaching insights text.
    /// </summary>
    public string Insights { get; set; } = string.Empty;

    /// <summary>
    /// Number of data items analyzed to generate the insights.
    /// </summary>
    public int ItemsAnalyzed { get; set; }

    /// <summary>
    /// Whether the insights generation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if generation failed.
    /// </summary>
    public string? Error { get; set; }
}
