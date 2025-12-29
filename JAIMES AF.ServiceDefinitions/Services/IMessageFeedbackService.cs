using MattEland.Jaimes.Domain;

namespace MattEland.Jaimes.ServiceDefinitions.Services;

public interface IMessageFeedbackService
{
    Task<MessageFeedbackDto> SubmitFeedbackAsync(int messageId, bool isPositive, string? comment, CancellationToken cancellationToken = default);
    Task<MessageFeedbackDto?> GetFeedbackForMessageAsync(int messageId, CancellationToken cancellationToken = default);
}

