using MattEland.Jaimes.Domain;

namespace MattEland.Jaimes.ServiceDefinitions.Services;

public interface IMessageToolCallService
{
    Task<IReadOnlyList<MessageToolCallDto>> GetToolCallsForMessageAsync(int messageId, CancellationToken cancellationToken = default);
}


