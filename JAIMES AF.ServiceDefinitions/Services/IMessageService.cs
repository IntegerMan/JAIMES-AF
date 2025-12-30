using MattEland.Jaimes.Domain;

namespace MattEland.Jaimes.ServiceDefinitions.Services;

public interface IMessageService
{
    /// <summary>
    /// Retrieves a list of messages representing the context leading up to a specific message.
    /// </summary>
    /// <param name="messageId">The ID of the target message (which will be the last message in the returned list).</param>
    /// <param name="count">The maximum number of messages to retrieve.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of MessageDto objects.</returns>
    Task<IEnumerable<MessageDto>> GetMessageContextAsync(int messageId, int count, CancellationToken cancellationToken = default);
}
