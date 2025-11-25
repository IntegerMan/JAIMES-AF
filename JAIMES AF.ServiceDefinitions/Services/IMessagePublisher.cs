namespace MattEland.Jaimes.ServiceDefinitions.Services;

/// <summary>
/// Service for publishing messages to LavinMQ/RabbitMQ
/// </summary>
public interface IMessagePublisher
{
    /// <summary>
    /// Publishes a message to the message queue
    /// </summary>
    /// <typeparam name="T">The message type</typeparam>
    /// <param name="message">The message to publish</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class;
}

