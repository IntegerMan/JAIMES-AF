using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.Extensions.AI.Evaluation;

namespace MattEland.Jaimes.ApiService.Services;

/// <summary>
/// Service for calculating expected evaluation metric counts based on registered evaluators.
/// </summary>
public class EvaluatorMetricCountService(IEnumerable<IEvaluator> evaluators) : IEvaluatorMetricCountService
{
    private readonly Lazy<int> _totalExpectedMetricCount = new(() => CalculateTotalExpectedMetrics(evaluators));

    /// <summary>
    /// Calculates the expected number of metrics for an evaluator.
    /// RulesTextConsistencyEvaluator produces 3 metrics, all others produce 1.
    /// </summary>
    private static int GetExpectedMetricCount(IEvaluator evaluator)
    {
        // RulesTextConsistencyEvaluator is a special case that produces 3 metrics
        return evaluator.GetType().Name == "RulesTextConsistencyEvaluator" ? 3 : 1;
    }

    /// <summary>
    /// Calculates the total expected metrics for a collection of evaluators.
    /// </summary>
    private static int CalculateTotalExpectedMetrics(IEnumerable<IEvaluator> evaluatorList)
    {
        return evaluatorList.Sum(GetExpectedMetricCount);
    }

    /// <inheritdoc />
    public int GetTotalExpectedMetricCount() => _totalExpectedMetricCount.Value;
}
