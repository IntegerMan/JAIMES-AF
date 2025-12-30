using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.ServiceDefinitions.Services;

/// <summary>
/// Service for retrieving tool usage statistics.
/// </summary>
public interface IToolUsageService
{
    /// <summary>
    /// Gets paginated tool usage statistics with optional filtering.
    /// </summary>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="agentId">Optional agent ID to filter by.</param>
    /// <param name="instructionVersionId">Optional instruction version ID to filter by.</param>
    /// <param name="gameId">Optional game ID to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated list of tool usage statistics.</returns>
    Task<ToolUsageListResponse> GetToolUsageAsync(
        int page,
        int pageSize,
        string? agentId = null,
        int? instructionVersionId = null,
        Guid? gameId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets paginated tool call details for a specific tool.
    /// </summary>
    /// <param name="toolName">The name of the tool to get details for.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="agentId">Optional agent ID to filter by.</param>
    /// <param name="instructionVersionId">Optional instruction version ID to filter by.</param>
    /// <param name="gameId">Optional game ID to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated list of tool call details.</returns>
    Task<ToolCallDetailListResponse> GetToolCallDetailsAsync(
        string toolName,
        int page,
        int pageSize,
        string? agentId = null,
        int? instructionVersionId = null,
        Guid? gameId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets full details for a specific tool call.
    /// </summary>
    /// <param name="toolCallId">The ID of the tool call.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The full details of the tool call, or null if not found.</returns>
    Task<ToolCallFullDetailResponse?> GetToolCallFullDetailsAsync(int toolCallId,
        CancellationToken cancellationToken = default);
}

