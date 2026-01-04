namespace MattEland.Jaimes.ServiceDefinitions.Messages;

/// <summary>
/// Message representing a single evaluator task for parallel evaluation processing.
/// Enables distribution of individual evaluators across multiple worker instances.
/// </summary>
public class EvaluatorTaskMessage
{
    /// <summary>
    /// The database ID of the message to evaluate.
    /// </summary>
    public int MessageId { get; set; }

    /// <summary>
    /// The game ID associated with the message.
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// The class name of the evaluator to run (e.g., "BrevityEvaluator").
    /// </summary>
    public required string EvaluatorName { get; set; }

    /// <summary>
    /// The 1-based index of this evaluator in the batch (for progress tracking).
    /// </summary>
    public int EvaluatorIndex { get; set; }

    /// <summary>
    /// The total number of evaluators in this batch (for progress tracking).
    /// </summary>
    public int TotalEvaluators { get; set; }

    /// <summary>
    /// Unique identifier for this evaluation batch, used to correlate results.
    /// </summary>
    public Guid BatchId { get; set; }
}
