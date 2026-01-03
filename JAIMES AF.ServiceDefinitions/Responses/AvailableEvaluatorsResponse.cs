namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Response containing the list of available evaluator names.
/// These are dynamically discovered from registered IEvaluator implementations.
/// </summary>
public class AvailableEvaluatorsResponse
{
    /// <summary>
    /// Gets or sets the list of available evaluator names.
    /// </summary>
    public List<string> EvaluatorNames { get; set; } = [];
}
