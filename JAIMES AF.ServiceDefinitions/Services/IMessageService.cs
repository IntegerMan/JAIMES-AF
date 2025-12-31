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
    Task<IEnumerable<MessageContextDto>> GetMessageContextAsync(int messageId, int countBefore, int countAfter,
        CancellationToken cancellationToken = default);
}
