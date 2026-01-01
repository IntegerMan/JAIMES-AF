namespace MattEland.Jaimes.ServiceDefinitions.Requests;

/// <summary>
/// Request to generate AI insights for a specific data type.
/// </summary>
public class GenerateInsightsRequest
{
    /// <summary>
    /// Type of insight to generate: "feedback", "metrics", or "sentiment".
    /// </summary>
    public string InsightType { get; set; } = string.Empty;
}
