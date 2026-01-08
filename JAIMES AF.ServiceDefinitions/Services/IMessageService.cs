using MattEland.Jaimes.Domain;
using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.ServiceDefinitions.Services;

public interface IMessageService
{
    /// <summary>
    /// Retrieves a list of messages representing the context leading up to a specific message.
    /// </summary>
    /// <param name="messageId">The ID of the target message.</param>
    /// <param name="countBefore">The maximum number of messages to retrieve before the target message.</param>
    /// <param name="countAfter">The maximum number of messages to retrieve after the target message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of MessageDto objects.</returns>
    Task<IEnumerable<MessageContextDto>> GetMessageContextAsync(int messageId,
        int countBefore,
        int countAfter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a list of non-scripted messages with optional filtering by agent and version.
    /// </summary>
    /// <param name="agentId">The optional ID of the agent.</param>
    /// <param name="versionId">The optional instruction version ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of MessageContextDto objects.</returns>
    Task<IEnumerable<MessageContextDto>> GetMessagesByAgentAsync(string? agentId,
        int? versionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves JSONL export data for a specific agent version, pairing user queries with agent responses.
    /// </summary>
    /// <param name="agentId">The ID of the agent.</param>
    /// <param name="versionId">The instruction version ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of JsonlExportRecord objects containing paired query-response data.</returns>
    Task<IEnumerable<JsonlExportRecord>> GetJsonlExportDataAsync(string agentId,
        int versionId,
        CancellationToken cancellationToken = default);
}
