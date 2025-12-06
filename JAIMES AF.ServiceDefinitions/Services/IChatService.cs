namespace MattEland.Jaimes.ServiceDefinitions.Services;

public interface IChatService
{
    Task<JaimesChatResponse> ProcessChatMessageAsync(GameDto game,
        string message,
        CancellationToken cancellationToken = default);

    Task<InitialMessageResponse> GenerateInitialMessageAsync(GenerateInitialMessageRequest request,
        CancellationToken cancellationToken = default);
}