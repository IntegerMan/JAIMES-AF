using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MattEland.Jaimes.Repositories.Entities;

/// <summary>
/// Stores evaluation metrics from test case runs.
/// </summary>
public class TestCaseRunMetric
{
    public int Id { get; set; }

    public int TestCaseRunId { get; set; }
    public TestCaseRun? TestCaseRun { get; set; }

    [MaxLength(100)] public required string MetricName { get; set; }

    public double Score { get; set; }

    [Column(TypeName = "text")] public string? Remarks { get; set; }

    [Required] public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Diagnostic data as a JSON string with additional evaluation context.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string? Diagnostics { get; set; }

    public int? EvaluatorId { get; set; }
    public Evaluator? Evaluator { get; set; }

    public int? EvaluationModelId { get; set; }

    [ForeignKey(nameof(EvaluationModelId))]
    public Model? EvaluationModel { get; set; }
}
