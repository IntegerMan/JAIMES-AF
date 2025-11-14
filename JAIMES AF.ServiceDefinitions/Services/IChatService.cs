using MattEland.Jaimes.Domain;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.ServiceDefinitions.Services;

public interface IChatService
{
    Task<ChatResponse> ProcessChatMessageAsync(GameDto game, string message, CancellationToken cancellationToken = default);
    Task<InitialMessageResponse> GenerateInitialMessageAsync(GenerateInitialMessageRequest request, CancellationToken cancellationToken = default);
}