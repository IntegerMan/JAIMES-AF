using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Services;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Configure OpenTelemetry for Aspire telemetry
builder.ConfigureOpenTelemetry();

// Configure logging with OpenTelemetry
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
});

// Load configuration
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", false, false)
    .AddJsonFile("appsettings.Development.json", true, false)
    .AddUserSecrets(typeof(Program).Assembly)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

// Add PostgreSQL with EF Core
builder.Services.AddJaimesRepositories(builder.Configuration);

// Configure message consuming and publishing using RabbitMQ.Client (LavinMQ compatible)
IConnectionFactory connectionFactory = RabbitMqConnectionFactory.CreateConnectionFactory(builder.Configuration);
builder.Services.AddSingleton(connectionFactory);
builder.Services.AddSingleton<IMessagePublisher, MessagePublisher>();

// Register consumer
builder.Services.AddSingleton<IMessageConsumer<ConversationMessageQueuedMessage>, UserMessageConsumer>();

// Register consumer service (background service) with role-based routing
builder.Services.AddHostedService(serviceProvider =>
{
    IConnectionFactory factory = serviceProvider.GetRequiredService<IConnectionFactory>();
    IMessageConsumer<ConversationMessageQueuedMessage> consumer = serviceProvider.GetRequiredService<IMessageConsumer<ConversationMessageQueuedMessage>>();
    ILogger<RoleBasedMessageConsumerService<ConversationMessageQueuedMessage>> logger = serviceProvider.GetRequiredService<ILogger<RoleBasedMessageConsumerService<ConversationMessageQueuedMessage>>>();
    ActivitySource? activitySource = serviceProvider.GetService<ActivitySource>();
    return new RoleBasedMessageConsumerService<ConversationMessageQueuedMessage>(
        factory,
        consumer,
        logger,
        "user",
        activitySource);
});

// Configure OpenTelemetry ActivitySource
const string activitySourceName = "Jaimes.Workers.UserMessageWorker";
ActivitySource activitySource = new(activitySourceName);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(activitySourceName))
    .WithMetrics(metrics =>
    {
        metrics.AddRuntimeInstrumentation()
            .AddHttpClientInstrumentation()
            .AddMeter(activitySourceName);
    })
    .WithTracing(tracing =>
    {
        tracing.AddSource(activitySourceName)
            .AddHttpClientInstrumentation();
    });

// Register ActivitySource for dependency injection
builder.Services.AddSingleton(activitySource);

// Build host
using IHost host = builder.Build();

ILogger<Program> logger = host.Services.GetRequiredService<ILogger<Program>>();

await host.InitializeDatabaseAsync();

logger.LogInformation("Starting User Message Worker");

await host.RunAsync();

