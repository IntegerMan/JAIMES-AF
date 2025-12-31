using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.ServiceDefinitions.Services;

/// <summary>
/// Service for retrieving evaluator data and statistics.
/// </summary>
public interface IEvaluatorService
{
    /// <summary>
    /// Gets paginated evaluator data with aggregate statistics.
    /// </summary>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="agentId">Optional agent ID to filter by.</param>
    /// <param name="instructionVersionId">Optional instruction version ID to filter by.</param>
    /// <param name="gameId">Optional game ID to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated list of evaluators with statistics.</returns>
    Task<EvaluatorListResponse> GetEvaluatorsAsync(
        int page,
        int pageSize,
        string? agentId = null,
        int? instructionVersionId = null,
        Guid? gameId = null,
        CancellationToken cancellationToken = default);
}
