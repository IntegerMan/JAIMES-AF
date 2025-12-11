namespace MattEland.Jaimes.ServiceDefinitions.Services;

public interface IChatService
{
    Task<InitialMessageResponse> GenerateInitialMessageAsync(GenerateInitialMessageRequest request,
        CancellationToken cancellationToken = default);
}