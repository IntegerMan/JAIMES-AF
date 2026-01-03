namespace MattEland.Jaimes.ServiceDefinitions.Requests;

/// <summary>
/// Request to update the pipeline queue size for a specific stage.
/// </summary>
public class UpdatePipelineQueueSizeRequest
{
    /// <summary>
    /// The pipeline stage (cracking, chunking, or embedding).
    /// </summary>
    public required string Stage { get; init; }

    /// <summary>
    /// The current queue size.
    /// </summary>
    public required int QueueSize { get; init; }

    /// <summary>
    /// Optional identifier for the worker reporting this status.
    /// </summary>
    public string? WorkerSource { get; init; }
}
