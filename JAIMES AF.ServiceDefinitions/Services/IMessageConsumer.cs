namespace MattEland.Jaimes.ServiceDefinitions.Services;

/// <summary>
/// Interface for message consumers
/// </summary>
/// <typeparam name="T">The message type to consume</typeparam>
public interface IMessageConsumer<in T> where T : class
{
    /// <summary>
    /// Handles a consumed message
    /// </summary>
    /// <param name="message">The message to handle</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task HandleAsync(T message, CancellationToken cancellationToken = default);
}

