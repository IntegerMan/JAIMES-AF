using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.ServiceDefinitions.Services;

/// <summary>
/// Service interface for tracking and broadcasting document pipeline status.
/// </summary>
public interface IPipelineStatusService
{
    /// <summary>
    /// Gets the current pipeline status including queue sizes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Current pipeline status notification.</returns>
    Task<PipelineStatusNotification> GetCurrentStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the queue size for a specific pipeline stage and broadcasts the update.
    /// </summary>
    /// <param name="stage">The pipeline stage (e.g., "cracking", "chunking", "embedding").</param>
    /// <param name="queueSize">The current queue size.</param>
    /// <param name="workerSource">Optional identifier for the worker reporting this status.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateQueueSizeAsync(string stage, int queueSize, string? workerSource = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the queue sizes directly from RabbitMQ.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Current pipeline status notification with queue sizes from RabbitMQ.</returns>
    Task<PipelineStatusNotification> GetQueueSizesFromBrokerAsync(CancellationToken cancellationToken = default);
}
