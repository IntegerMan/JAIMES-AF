using Microsoft.Extensions.AI.Evaluation;

namespace MattEland.Jaimes.ServiceDefinitions.Services;

/// <summary>
/// Shared helper for calculating expected evaluation metric counts.
/// This ensures consistent metric counting logic across API and worker services.
/// </summary>
public static class EvaluatorMetricCountHelper
{
    /// <summary>
    /// Calculates the expected number of metrics for an evaluator.
    /// RelevanceTruthAndCompletenessEvaluator produces 3 metrics (Relevance, Truth, Completeness), all others produce 1.
    /// </summary>
    public static int GetExpectedMetricCount(IEvaluator evaluator)
    {
        // RelevanceTruthAndCompletenessEvaluator is a special case that produces 3 metrics
        return evaluator.GetType().Name == "RelevanceTruthAndCompletenessEvaluator" ? 3 : 1;
    }

    /// <summary>
    /// Calculates the total expected metrics for a collection of evaluators.
    /// </summary>
    public static int CalculateTotalExpectedMetrics(IEnumerable<IEvaluator> evaluatorList)
    {
        return evaluatorList.Sum(GetExpectedMetricCount);
    }
}
