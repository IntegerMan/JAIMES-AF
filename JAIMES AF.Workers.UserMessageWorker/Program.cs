using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Workers.UserMessageWorker.Options;
using MattEland.Jaimes.Workers.UserMessageWorker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Add service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

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

// Configure sentiment analysis options
builder.Services.Configure<SentimentAnalysisOptions>(
    builder.Configuration.GetSection(SentimentAnalysisOptions.SectionName));

// Register sentiment model service as singleton with dependencies for reclassification
builder.Services.AddSingleton<SentimentModelService>(serviceProvider =>
{
    ILogger<SentimentModelService> logger = serviceProvider.GetRequiredService<ILogger<SentimentModelService>>();
    IDbContextFactory<JaimesDbContext> contextFactory =
        serviceProvider.GetRequiredService<IDbContextFactory<JaimesDbContext>>();
    IOptions<SentimentAnalysisOptions> options =
        serviceProvider.GetRequiredService<IOptions<SentimentAnalysisOptions>>();
    return new SentimentModelService(logger, contextFactory, options);
});

// Configure message consuming and publishing using RabbitMQ.Client (LavinMQ compatible)
IConnectionFactory connectionFactory = RabbitMqConnectionFactory.CreateConnectionFactory(builder.Configuration);
builder.Services.AddSingleton(connectionFactory);
builder.Services.AddSingleton<IMessagePublisher, MessagePublisher>();

// Configure HttpClient for API service (for SignalR notifications)
builder.Services.AddHttpClient<IMessageUpdateNotifier, MessageUpdateNotifier>(client =>
{
    // Use Aspire service discovery - the connection string will be injected by AppHost
    string baseAddress = builder.Configuration.GetConnectionString("jaimes-api") ?? "http://jaimes-api";
    client.BaseAddress = new Uri(baseAddress);
});

// Register consumer
builder.Services.AddSingleton<IMessageConsumer<ConversationMessageQueuedMessage>, UserMessageConsumer>();

// Register consumer service (background service) with role-based routing
builder.Services.AddHostedService(serviceProvider =>
{
    IConnectionFactory factory = serviceProvider.GetRequiredService<IConnectionFactory>();
    IMessageConsumer<ConversationMessageQueuedMessage> consumer =
        serviceProvider.GetRequiredService<IMessageConsumer<ConversationMessageQueuedMessage>>();
    ILogger<RoleBasedMessageConsumerService<ConversationMessageQueuedMessage>> logger = serviceProvider
        .GetRequiredService<ILogger<RoleBasedMessageConsumerService<ConversationMessageQueuedMessage>>>();
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

await host.WaitForMigrationsAsync();

// Load or train sentiment model before starting message processing
logger.LogInformation("Loading or training sentiment analysis model...");
SentimentModelService sentimentModelService = host.Services.GetRequiredService<SentimentModelService>();
await sentimentModelService.LoadOrTrainModelAsync();
logger.LogInformation("Sentiment analysis model ready");

// Reclassify all user messages on startup if configured
IOptions<SentimentAnalysisOptions> sentimentOptions =
    host.Services.GetRequiredService<IOptions<SentimentAnalysisOptions>>();
if (sentimentOptions.Value.ReclassifyAllUserMessagesOnStartup)
{
    logger.LogInformation("ReclassifyAllUserMessagesOnStartup is enabled. Reclassifying all user messages...");
    await sentimentModelService.ReclassifyAllUserMessagesAsync();
    logger.LogInformation("User message reclassification completed");
}
else
{
    logger.LogInformation("ReclassifyAllUserMessagesOnStartup is disabled. Skipping reclassification.");
}

logger.LogInformation("Starting User Message Worker");

await host.RunAsync();

