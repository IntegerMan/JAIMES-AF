namespace MattEland.Jaimes.Repositories.Entities;

/// <summary>
/// Stores evaluation metrics from test case runs.
/// </summary>
public class TestCaseRunMetric
{
    public int Id { get; set; }

    public int TestCaseRunId { get; set; }
    public TestCaseRun? TestCaseRun { get; set; }

    [MaxLength(100)]
    public required string MetricName { get; set; }

    public double Score { get; set; }

    [MaxLength(2000)]
    public string? Remarks { get; set; }

    public int? EvaluatorId { get; set; }
    public Evaluator? Evaluator { get; set; }
}
