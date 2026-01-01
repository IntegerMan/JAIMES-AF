namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Response DTO indicating which evaluators are missing for a message.
/// </summary>
public class MissingEvaluatorsResponse
{
    /// <summary>
    /// The message ID being queried.
    /// </summary>
    public int MessageId { get; set; }

    /// <summary>
    /// List of evaluator class names that have not been run for this message.
    /// </summary>
    public List<string> MissingEvaluators { get; set; } = [];

    /// <summary>
    /// Total number of evaluators registered in the system.
    /// </summary>
    public int TotalRegisteredEvaluators { get; set; }

    /// <summary>
    /// Number of evaluation metrics already present for this message.
    /// </summary>
    public int ExistingMetricsCount { get; set; }

    /// <summary>
    /// True if the message is eligible for evaluation (not scripted).
    /// </summary>
    public bool IsEligibleForEvaluation { get; set; }
}
