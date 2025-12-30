using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.ServiceDefinitions.Services;

/// <summary>
/// Service interface for evaluation metrics operations.
/// </summary>
public interface IMessageEvaluationMetricsService
{
    /// <summary>
    /// Gets a paginated list of evaluation metrics with optional filtering.
    /// </summary>
    /// <param name="page">The page number (1-based).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="filters">Optional filter parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated list of evaluation metrics.</returns>
    Task<EvaluationMetricListResponse> GetMetricsListAsync(
        int page,
        int pageSize,
        AdminFilterParams? filters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all evaluation metrics for a specific message.
    /// </summary>
    /// <param name="messageId">The message ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of evaluation metrics for the message.</returns>
    Task<List<MessageEvaluationMetricResponse>> GetMetricsForMessageAsync(
        int messageId,
        CancellationToken cancellationToken = default);
}
