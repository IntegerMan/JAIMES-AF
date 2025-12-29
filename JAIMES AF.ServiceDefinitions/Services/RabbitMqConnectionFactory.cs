namespace MattEland.Jaimes.ServiceDefinitions.Services;

/// <summary>
/// Factory for creating RabbitMQ connection factories from configuration
/// </summary>
public static class RabbitMqConnectionFactory
{
    /// <summary>
    /// Creates a connection factory from configuration
    /// </summary>
    public static IConnectionFactory CreateConnectionFactory(IConfiguration configuration, ILogger? logger = null)
    {
        string? connectionString = configuration.GetConnectionString("LavinMQ-Messaging")
                                   ?? configuration["ConnectionStrings:LavinMQ-Messaging"]
                                   ?? configuration["ConnectionStrings__LavinMQ-Messaging"];

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "Messaging connection string is not configured. " +
                "Expected connection string 'LavinMQ-Messaging' from Aspire.");

        // Parse connection string (format: amqp://username:password@host:port/vhost)
        Uri rabbitUri = new(connectionString);
        string host = rabbitUri.Host;
        ushort port = rabbitUri.Port > 0 ? (ushort)rabbitUri.Port : (ushort)5672;
        string? username = null;
        string? password = null;

        if (!string.IsNullOrEmpty(rabbitUri.UserInfo))
        {
            string[] userInfo = rabbitUri.UserInfo.Split(':');
            username = userInfo[0];
            if (userInfo.Length > 1) password = userInfo[1];
        }

        ConnectionFactory factory = new()
        {
            HostName = host,
            Port = port,
            UserName = username ?? "guest",
            Password = password ?? "guest",
            VirtualHost = "/",
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
        };

        logger?.LogInformation("Created RabbitMQ connection factory for {Host}:{Port}", host, port);

        return factory;
    }
}