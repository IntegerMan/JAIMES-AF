using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.ServiceDefinitions.Services;

/// <summary>
/// Service interface for sentiment operations.
/// </summary>
public interface IMessageSentimentService
{
    /// <summary>
    /// Gets a paginated list of sentiment records with optional filtering.
    /// </summary>
    /// <param name="page">The page number (1-based).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="filters">Optional filter parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated list of sentiment records.</returns>
    Task<SentimentListResponse> GetSentimentListAsync(
        int page,
        int pageSize,
        AdminFilterParams? filters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the full details for a specific sentiment record.
    /// </summary>
    /// <param name="id">The sentiment record ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The sentiment details or null if not found.</returns>
    Task<SentimentFullDetailsResponse?> GetSentimentDetailsAsync(
        int id,
        CancellationToken cancellationToken = default);
}
