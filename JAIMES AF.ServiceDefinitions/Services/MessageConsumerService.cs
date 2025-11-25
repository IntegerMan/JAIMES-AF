using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace MattEland.Jaimes.ServiceDefinitions.Services;

/// <summary>
/// Background service that consumes messages from RabbitMQ/LavinMQ
/// </summary>
/// <typeparam name="T">The message type to consume</typeparam>
public class MessageConsumerService<T> : BackgroundService where T : class
{
    private readonly IConnectionFactory _connectionFactory;
    private readonly IMessageConsumer<T> _messageHandler;
    private readonly ILogger<MessageConsumerService<T>> _logger;
    private readonly ActivitySource? _activitySource;
    private readonly JsonSerializerOptions _jsonOptions;
    private IConnection? _connection;
    private IChannel? _channel;
    private string? _consumerTag;

    public MessageConsumerService(
        IConnectionFactory connectionFactory,
        IMessageConsumer<T> messageHandler,
        ILogger<MessageConsumerService<T>> logger,
        ActivitySource? activitySource = null)
    {
        _connectionFactory = connectionFactory;
        _messageHandler = messageHandler;
        _logger = logger;
        _activitySource = activitySource;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        string messageTypeName = typeof(T).Name;
        string queueName = messageTypeName;
        string exchangeName = messageTypeName;

        try
        {
            _connection = await _connectionFactory.CreateConnectionAsync(stoppingToken);
            _channel = await _connection.CreateChannelAsync();

            // Declare exchange
            await _channel.ExchangeDeclareAsync(exchangeName, ExchangeType.Topic, durable: true, autoDelete: false,
                cancellationToken: stoppingToken);

            // Declare queue
            await _channel.QueueDeclareAsync(queueName, durable: true, exclusive: false, autoDelete: false,
                arguments: null, cancellationToken: stoppingToken);

            // Bind queue to exchange
            string routingKey = messageTypeName;
            await _channel.QueueBindAsync(queueName, exchangeName, routingKey, arguments: null,
                cancellationToken: stoppingToken);

            // Set QoS to process one message at a time
            await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false,
                cancellationToken: stoppingToken);

            // Create async consumer
            MessageHandlerConsumer consumer = new(_channel, _messageHandler, _logger, _activitySource, _jsonOptions,
                messageTypeName, stoppingToken);

            _consumerTag = await _channel.BasicConsumeAsync(queueName, autoAck: false, consumer: consumer,
                cancellationToken: stoppingToken);

            _logger.LogInformation("Started consuming messages of type {MessageType} from queue {QueueName}",
                messageTypeName, queueName);

            // Keep the service running
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in message consumer service for {MessageType}", messageTypeName);
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_consumerTag) && _channel != null && _channel.IsOpen)
        {
            await _channel.BasicCancelAsync(_consumerTag, cancellationToken: cancellationToken);
        }

        _channel?.Dispose();
        _connection?.Dispose();

        await base.StopAsync(cancellationToken);
    }

    private class MessageHandlerConsumer : IAsyncBasicConsumer
    {
        private readonly IChannel _channel;
        private readonly IMessageConsumer<T> _messageHandler;
        private readonly ILogger<MessageConsumerService<T>> _logger;
        private readonly ActivitySource? _activitySource;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly string _messageTypeName;
        private readonly CancellationToken _stoppingToken;

        public MessageHandlerConsumer(
            IChannel channel,
            IMessageConsumer<T> messageHandler,
            ILogger<MessageConsumerService<T>> logger,
            ActivitySource? activitySource,
            JsonSerializerOptions jsonOptions,
            string messageTypeName,
            CancellationToken stoppingToken)
        {
            _channel = channel;
            _messageHandler = messageHandler;
            _logger = logger;
            _activitySource = activitySource;
            _jsonOptions = jsonOptions;
            _messageTypeName = messageTypeName;
            _stoppingToken = stoppingToken;
        }

        public IChannel Channel => _channel;

        public event Func<object, object, CancellationToken, Task>? ConsumerCancelled
        {
            add { }
            remove { }
        }

        public Task HandleBasicCancelAsync(string consumerTag, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task HandleBasicCancelOkAsync(string consumerTag, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task HandleBasicConsumeOkAsync(string consumerTag, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public async Task HandleBasicDeliverAsync(
            string consumerTag,
            ulong deliveryTag,
            bool redelivered,
            string exchange,
            string routingKey,
            IReadOnlyBasicProperties properties,
            ReadOnlyMemory<byte> body,
            CancellationToken cancellationToken = default)
        {
            string? messageId = properties.MessageId ?? Guid.NewGuid().ToString();
            using Activity? activity =
                _activitySource?.StartActivity($"MessageConsumer.Consume.{_messageTypeName}");
            activity?.SetTag("messaging.message_id", messageId);
            activity?.SetTag("messaging.message_type", _messageTypeName);

            try
            {
                string messageJson = Encoding.UTF8.GetString(body.Span);

                T? message = JsonSerializer.Deserialize<T>(messageJson, _jsonOptions);
                if (message == null)
                {
                    _logger.LogError("Failed to deserialize message of type {MessageType}. MessageId: {MessageId}",
                        _messageTypeName, messageId);
                    activity?.SetStatus(ActivityStatusCode.Error, "Deserialization failed");
                    await _channel.BasicNackAsync(deliveryTag, false, false);
                    return;
                }

                _logger.LogDebug("Received message of type {MessageType}. MessageId: {MessageId}",
                    _messageTypeName, messageId);

                int maxRetries = 5;
                int retryCount = 0;
                bool success = false;

                while (retryCount < maxRetries && !_stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        await _messageHandler.HandleAsync(message, _stoppingToken);
                        success = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        retryCount++;
                        _logger.LogWarning(ex,
                            "Error processing message of type {MessageType}. Retry {RetryCount}/{MaxRetries}. MessageId: {MessageId}",
                            _messageTypeName, retryCount, maxRetries, messageId);

                        if (retryCount < maxRetries)
                        {
                            int delayMs = (int)Math.Pow(2, retryCount) * 1000;
                            await Task.Delay(delayMs, _stoppingToken);
                        }
                    }
                }

                if (success)
                {
                    await _channel.BasicAckAsync(deliveryTag, false);
                    activity?.SetStatus(ActivityStatusCode.Ok);
                    _logger.LogDebug("Successfully processed message of type {MessageType}. MessageId: {MessageId}",
                        _messageTypeName, messageId);
                }
                else
                {
                    _logger.LogError(
                        "Failed to process message of type {MessageType} after {MaxRetries} retries. MessageId: {MessageId}",
                        _messageTypeName, maxRetries, messageId);
                    activity?.SetStatus(ActivityStatusCode.Error, "Max retries exceeded");
                    await _channel.BasicNackAsync(deliveryTag, false, false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unexpected error processing message of type {MessageType}. MessageId: {MessageId}",
                    _messageTypeName, messageId);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                await _channel.BasicNackAsync(deliveryTag, false, true);
            }
        }

        public Task HandleChannelShutdownAsync(object channel, RabbitMQ.Client.Events.ShutdownEventArgs reason)
        {
            throw new NotImplementedException();
        }
    }
}