using MattEland.Jaimes.Domain;
using MattEland.Jaimes.ServiceDefinitions.Requests;

namespace MattEland.Jaimes.ServiceDefinitions.Services;

public interface IMessageFeedbackService
{
    Task<MessageFeedbackDto> SubmitFeedbackAsync(int messageId,
        bool isPositive,
        string? comment,
        CancellationToken cancellationToken = default);

    Task<MessageFeedbackDto?> GetFeedbackForMessageAsync(int messageId, CancellationToken cancellationToken = default);

    Task<MattEland.Jaimes.ServiceDefinitions.Responses.FeedbackListResponse> GetFeedbackListAsync(int page,
        int pageSize,
        AdminFilterParams? filters = null,
        CancellationToken cancellationToken = default);

    Task<MattEland.Jaimes.ServiceDefinitions.Responses.FeedbackFullDetailsResponse?> GetFeedbackDetailsAsync(int id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets feedback summary statistics with optional filters.
    /// </summary>
    Task<MattEland.Jaimes.ServiceDefinitions.Responses.FeedbackSummaryResponse> GetFeedbackSummaryAsync(
        AdminFilterParams? filters = null,
        CancellationToken cancellationToken = default);
}


