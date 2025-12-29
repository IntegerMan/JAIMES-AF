using MattEland.Jaimes.ServiceDefinitions.Messages;

namespace MattEland.Jaimes.ServiceDefinitions.Services;

/// <summary>
/// Implementation of IMessagePublisher using RabbitMQ.Client (compatible with LavinMQ)
/// </summary>
public class MessagePublisher : IMessagePublisher, IDisposable
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly ILogger<MessagePublisher> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed;

    public MessagePublisher(IConnectionFactory connectionFactory, ILogger<MessagePublisher> logger)
    {
        _logger = logger;
        _connection = connectionFactory.CreateConnectionAsync().GetAwaiter().GetResult();
        _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
    {
        if (_disposed) throw new ObjectDisposedException(nameof(MessagePublisher));

        try
        {
            // Get the message type name for exchange/queue naming
            string messageTypeName = typeof(T).Name;

            // Declare exchange (topic exchange for routing)
            string exchangeName = messageTypeName;
            await _channel.ExchangeDeclareAsync(exchangeName,
                ExchangeType.Topic,
                true,
                false,
                cancellationToken: cancellationToken);

            // Serialize message to JSON
            string jsonMessage = JsonSerializer.Serialize(message, _jsonOptions);
            ReadOnlyMemory<byte> body = new(Encoding.UTF8.GetBytes(jsonMessage));

            // Create properties
            BasicProperties properties = new()
            {
                Persistent = true,
                ContentType = "application/json",
                Type = messageTypeName,
                MessageId = Guid.NewGuid().ToString()
            };

            // Determine routing key based on message type
            // For ConversationMessageQueuedMessage, use role-based routing keys
            string routingKey = messageTypeName;
            if (message is ConversationMessageQueuedMessage conversationMessage)
            {
                string roleSuffix = conversationMessage.Role.ToString().ToLowerInvariant();
                routingKey = $"{messageTypeName}.{roleSuffix}";
                _logger.LogDebug("Using role-based routing key {RoutingKey} for {Role} message",
                    routingKey,
                    conversationMessage.Role);
            }

            await _channel.BasicPublishAsync(
                exchangeName,
                routingKey,
                false,
                properties,
                body,
                cancellationToken);

            _logger.LogDebug("Published message of type {MessageType} to exchange {Exchange} with routing key {RoutingKey}",
                messageTypeName,
                exchangeName,
                routingKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message of type {MessageType}", typeof(T).Name);
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _channel?.Dispose();
        _connection?.Dispose();
        _disposed = true;
    }
}