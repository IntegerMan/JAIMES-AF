using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.Services.Services;
using MattEland.Jaimes.Workers.UserMessageWorker.Consumers;
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

// Register classification model service
builder.Services.AddScoped<IClassificationModelService, ClassificationModelService>();

// Register sentiment model service as singleton with dependencies for reclassification
builder.Services.AddSingleton<SentimentModelService>(serviceProvider =>
{
    ILogger<SentimentModelService> logger = serviceProvider.GetRequiredService<ILogger<SentimentModelService>>();
    IServiceScopeFactory scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
    IDbContextFactory<JaimesDbContext> contextFactory =
        serviceProvider.GetRequiredService<IDbContextFactory<JaimesDbContext>>();
    IOptions<SentimentAnalysisOptions> options =
        serviceProvider.GetRequiredService<IOptions<SentimentAnalysisOptions>>();
    return new SentimentModelService(logger, scopeFactory, contextFactory, options);
});

// Register lightweight sentiment classification service for early classification
builder.Services.AddSingleton<ISentimentClassificationService, SentimentClassificationService>();

// Register pending sentiment cache for early classification correlation
builder.Services.AddSingleton<IPendingSentimentCache, MattEland.Jaimes.Services.Services.MemorySentimentCache>();

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

// Register classifier training service
builder.Services.AddSingleton<ClassifierTrainingService>();

// Register consumers
builder.Services.AddSingleton<IMessageConsumer<ConversationMessageQueuedMessage>, UserMessageConsumer>();
builder.Services.AddSingleton<IMessageConsumer<TrainClassifierMessage>, ClassifierTrainingConsumer>();
builder.Services
    .AddSingleton<IMessageConsumer<EarlySentimentClassificationMessage>, EarlySentimentClassificationConsumer>();

// Register consumer service for user messages (background service) with role-based routing
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

// Register consumer service for classifier training messages
builder.Services.AddHostedService(serviceProvider =>
{
    IConnectionFactory factory = serviceProvider.GetRequiredService<IConnectionFactory>();
    IMessageConsumer<TrainClassifierMessage> consumer =
        serviceProvider.GetRequiredService<IMessageConsumer<TrainClassifierMessage>>();
    ILogger<MessageConsumerService<TrainClassifierMessage>> logger = serviceProvider
        .GetRequiredService<ILogger<MessageConsumerService<TrainClassifierMessage>>>();
    return new MessageConsumerService<TrainClassifierMessage>(factory, consumer, logger);
});

// Register consumer service for early sentiment classification messages
builder.Services.AddHostedService(serviceProvider =>
{
    IConnectionFactory factory = serviceProvider.GetRequiredService<IConnectionFactory>();
    IMessageConsumer<EarlySentimentClassificationMessage> consumer =
        serviceProvider.GetRequiredService<IMessageConsumer<EarlySentimentClassificationMessage>>();
    ILogger<MessageConsumerService<EarlySentimentClassificationMessage>> logger = serviceProvider
        .GetRequiredService<ILogger<MessageConsumerService<EarlySentimentClassificationMessage>>>();
    return new MessageConsumerService<EarlySentimentClassificationMessage>(factory, consumer, logger);
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

// Pre-load lightweight sentiment classification model for early classification
logger.LogInformation("Pre-loading lightweight sentiment classification model...");
ISentimentClassificationService sentimentClassificationService =
    host.Services.GetRequiredService<ISentimentClassificationService>();
// Trigger initialization by classifying a dummy message
await sentimentClassificationService.ClassifyAsync("initialization", CancellationToken.None);
logger.LogInformation("Lightweight sentiment classification model ready");

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

