namespace MattEland.Jaimes.ServiceDefinitions.Responses;

public class MessageEvaluationMetricResponse
{
    public int Id { get; set; }
    public int MessageId { get; set; }
    public required string MetricName { get; set; }
    public double Score { get; set; }
    public string? Remarks { get; set; }
    public DateTime EvaluatedAt { get; set; }
    public string? Diagnostics { get; set; }
    public int? EvaluationModelId { get; set; }
}
