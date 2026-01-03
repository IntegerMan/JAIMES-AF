using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.ServiceDefinitions.Services;

/// <summary>
/// Service for discovering and executing tools for testing purposes.
/// </summary>
public interface IToolTestService
{
    /// <summary>
    /// Gets metadata about all registered tools that can be tested.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A list of tool metadata including parameter information.</returns>
    Task<ToolMetadataListResponse> GetRegisteredToolsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a tool for testing purposes.
    /// This method does NOT log the execution for diagnostics.
    /// </summary>
    /// <param name="request">The execution request containing tool name, game context, and parameters.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The result of the tool execution including timing information.</returns>
    Task<ToolExecutionResponse> ExecuteToolAsync(ToolExecutionRequest request, CancellationToken cancellationToken = default);
}
