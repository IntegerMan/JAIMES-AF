using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.Extensions.AI.Evaluation;

namespace MattEland.Jaimes.ApiService.Services;

/// <summary>
/// Service for calculating expected evaluation metric counts based on registered evaluators.
/// </summary>
public class EvaluatorMetricCountService(IEnumerable<IEvaluator> evaluators) : IEvaluatorMetricCountService
{
    private readonly Lazy<int> _totalExpectedMetricCount = new(() => EvaluatorMetricCountHelper.CalculateTotalExpectedMetrics(evaluators));

    /// <inheritdoc />
    public int GetTotalExpectedMetricCount() => _totalExpectedMetricCount.Value;
}
