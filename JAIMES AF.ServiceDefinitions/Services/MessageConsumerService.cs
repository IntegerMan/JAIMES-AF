namespace MattEland.Jaimes.ServiceDefinitions.Services;

/// <summary>
/// Background service that consumes messages from RabbitMQ/LavinMQ
/// </summary>
/// <typeparam name="T">The message type to consume</typeparam>
public class MessageConsumerService<T>(
    IConnectionFactory connectionFactory,
    IMessageConsumer<T> messageHandler,
    ILogger<MessageConsumerService<T>> logger,
    ActivitySource? activitySource = null,
    IPipelineStatusReporter? pipelineStatusReporter = null,
    string? pipelineStage = null)
    : BackgroundService
    where T : class
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    private IConnection? _connection;
    private IChannel? _channel;
    private string? _consumerTag;
    private static readonly TimeSpan StatusReportInterval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        string messageTypeName = typeof(T).Name;
        string queueName = messageTypeName;
        string exchangeName = messageTypeName;

        try
        {
            logger.LogInformation("Creating RabbitMQ connection for message type {MessageType}...", messageTypeName);
            _connection = await connectionFactory.CreateConnectionAsync(stoppingToken);
            logger.LogInformation("RabbitMQ connection established. Creating channel...");
            _channel = await _connection.CreateChannelAsync();
            logger.LogInformation("Channel created successfully.");

            // Declare exchange
            logger.LogInformation("Declaring exchange {ExchangeName} (type: Topic)...", exchangeName);
            await _channel.ExchangeDeclareAsync(exchangeName,
                ExchangeType.Topic,
                true,
                false,
                cancellationToken: stoppingToken);
            logger.LogInformation("Exchange {ExchangeName} declared successfully.", exchangeName);

            // Declare queue
            logger.LogInformation("Declaring queue {QueueName}...", queueName);
            QueueDeclareOk queueDeclareResult = await _channel.QueueDeclareAsync(queueName,
                true,
                false,
                false,
                null,
                cancellationToken: stoppingToken);
            logger.LogInformation(
                "Queue {QueueName} declared successfully. Current message count: {MessageCount}, Consumer count: {ConsumerCount}",
                queueName,
                queueDeclareResult.MessageCount,
                queueDeclareResult.ConsumerCount);

            // Bind queue to exchange
            string routingKey = messageTypeName;
            logger.LogInformation(
                "Binding queue {QueueName} to exchange {ExchangeName} with routing key {RoutingKey}...",
                queueName,
                exchangeName,
                routingKey);
            await _channel.QueueBindAsync(queueName,
                exchangeName,
                routingKey,
                null,
                cancellationToken: stoppingToken);
            logger.LogInformation("Queue {QueueName} bound to exchange {ExchangeName} with routing key {RoutingKey}.",
                queueName,
                exchangeName,
                routingKey);

            // Set QoS to process one message at a time
            await _channel.BasicQosAsync(0,
                1,
                false,
                stoppingToken);

            // Create async consumer
            MessageHandlerConsumer consumer = new(_channel,
                messageHandler,
                logger,
                activitySource,
                _jsonOptions,
                messageTypeName,
                stoppingToken);

            _consumerTag = await _channel.BasicConsumeAsync(queueName,
                false,
                consumer,
                stoppingToken);

            logger.LogInformation("Started consuming messages of type {MessageType} from queue {QueueName}",
                messageTypeName,
                queueName);

            // Log queue information for diagnostics
            QueueDeclareOk? queueInfo = await _channel.QueueDeclarePassiveAsync(queueName, stoppingToken);
            if (queueInfo != null)
                logger.LogInformation(
                    "Queue {QueueName} exists with {MessageCount} messages ready, {ConsumerCount} consumers",
                    queueName,
                    queueInfo.MessageCount,
                    queueInfo.ConsumerCount);

            // Keep the service running and periodically report queue status
            DateTimeOffset lastStatusReport = DateTimeOffset.MinValue;
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);

                // Report queue status periodically if a reporter is configured
                if (pipelineStatusReporter != null && !string.IsNullOrEmpty(pipelineStage) &&
                    DateTimeOffset.UtcNow - lastStatusReport >= StatusReportInterval)
                {
                    try
                    {
                        using IChannel statusChannel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);
                        QueueDeclareOk? statusQueueInfo = await statusChannel.QueueDeclarePassiveAsync(queueName, stoppingToken);
                        int currentQueueSize = (int)(statusQueueInfo?.MessageCount ?? 0);
                        
                        await pipelineStatusReporter.ReportQueueSizeAsync(pipelineStage, currentQueueSize, stoppingToken);
                        lastStatusReport = DateTimeOffset.UtcNow;
                        
                        logger.LogDebug("Reported queue size for {Stage}: {QueueSize}", pipelineStage, currentQueueSize);
                    }
                    catch (Exception ex)
                    {
                        logger.LogDebug(ex, "Failed to report queue status");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in message consumer service for {MessageType}", messageTypeName);
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_consumerTag) && _channel != null && _channel.IsOpen)
            await _channel.BasicCancelAsync(_consumerTag, cancellationToken: cancellationToken);

        _channel?.Dispose();
        _connection?.Dispose();

        await base.StopAsync(cancellationToken);
    }

    private class MessageHandlerConsumer(
        IChannel channel,
        IMessageConsumer<T> messageHandler,
        ILogger<MessageConsumerService<T>> logger,
        ActivitySource? activitySource,
        JsonSerializerOptions jsonOptions,
        string messageTypeName,
        CancellationToken stoppingToken)
        : IAsyncBasicConsumer
    {
        public IChannel Channel => channel;

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
            logger.LogInformation(
                "Consumer registration confirmed. ConsumerTag: {ConsumerTag}, MessageType: {MessageType}",
                consumerTag,
                messageTypeName);
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
            logger.LogInformation(
                "Received message delivery. ConsumerTag: {ConsumerTag}, DeliveryTag: {DeliveryTag}, Exchange: {Exchange}, RoutingKey: {RoutingKey}, MessageId: {MessageId}",
                consumerTag,
                deliveryTag,
                exchange,
                routingKey,
                messageId);

            using Activity? activity =
                activitySource?.StartActivity($"MessageConsumer.Consume.{messageTypeName}");
            activity?.SetTag("messaging.message_id", messageId);
            activity?.SetTag("messaging.message_type", messageTypeName);

            try
            {
                string messageJson = Encoding.UTF8.GetString(body.Span);
                logger.LogDebug(
                    "Deserializing message of type {MessageType}. MessageId: {MessageId}, BodyLength: {BodyLength}",
                    messageTypeName,
                    messageId,
                    body.Length);

                T? message = JsonSerializer.Deserialize<T>(messageJson, jsonOptions);
                if (message == null)
                {
                    logger.LogError("Failed to deserialize message of type {MessageType}. MessageId: {MessageId}",
                        messageTypeName,
                        messageId);
                    activity?.SetStatus(ActivityStatusCode.Error, "Deserialization failed");
                    await channel.BasicNackAsync(deliveryTag, false, false);
                    return;
                }

                logger.LogDebug("Received message of type {MessageType}. MessageId: {MessageId}",
                    messageTypeName,
                    messageId);

                int maxRetries = 5;
                int retryCount = 0;
                bool success = false;

                while (retryCount < maxRetries && !stoppingToken.IsCancellationRequested)
                    try
                    {
                        await messageHandler.HandleAsync(message, stoppingToken);
                        success = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        retryCount++;
                        logger.LogWarning(ex,
                            "Error processing message of type {MessageType}. Retry {RetryCount}/{MaxRetries}. MessageId: {MessageId}",
                            messageTypeName,
                            retryCount,
                            maxRetries,
                            messageId);

                        if (retryCount < maxRetries)
                        {
                            int delayMs = (int)Math.Pow(2, retryCount) * 1000;
                            await Task.Delay(delayMs, stoppingToken);
                        }
                    }

                if (success)
                {
                    await channel.BasicAckAsync(deliveryTag, false);
                    activity?.SetStatus(ActivityStatusCode.Ok);
                    logger.LogDebug("Successfully processed message of type {MessageType}. MessageId: {MessageId}",
                        messageTypeName,
                        messageId);
                }
                else
                {
                    logger.LogError(
                        "Failed to process message of type {MessageType} after {MaxRetries} retries. MessageId: {MessageId}",
                        messageTypeName,
                        maxRetries,
                        messageId);
                    activity?.SetStatus(ActivityStatusCode.Error, "Max retries exceeded");
                    await channel.BasicNackAsync(deliveryTag, false, false);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Unexpected error processing message of type {MessageType}. MessageId: {MessageId}",
                    messageTypeName,
                    messageId);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                await channel.BasicNackAsync(deliveryTag, false, true);
            }
        }

        public Task HandleChannelShutdownAsync(object channel, RabbitMQ.Client.Events.ShutdownEventArgs reason)
        {
            logger.LogWarning("Channel shutdown detected for consumer {MessageType}. Reason: {Reason}",
                messageTypeName,
                reason?.ToString() ?? "Unknown");
            return Task.CompletedTask;
        }
    }
}