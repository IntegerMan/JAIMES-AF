using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.ServiceDefinitions.Services;

public interface IConversationSearchService
{
    Task<ConversationSearchResponse> SearchConversationsAsync(
        Guid gameId,
        string query,
        int limit = 5,
        CancellationToken cancellationToken = default);
}

