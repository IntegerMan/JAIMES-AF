namespace MattEland.Jaimes.ServiceDefinitions.Services;

/// <summary>
/// Service for calculating expected evaluation metric counts.
/// </summary>
public interface IEvaluatorMetricCountService
{
    /// <summary>
    /// Gets the total number of expected metrics for all registered evaluators.
    /// </summary>
    int GetTotalExpectedMetricCount();
}
