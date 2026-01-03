using MattEland.Jaimes.Agents.Services;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.ServiceDefaults;
using MattEland.Jaimes.ServiceLayer;
using MattEland.Jaimes.Workers.AssistantMessageWorker.Services;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;
using MattEland.Jaimes.Evaluators;

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

// Add services (includes IInstructionService)
builder.Services.AddJaimesServices();

// Configure chat client for evaluation
builder.Services.AddChatClient(builder.Configuration, "TextGenerationModel");

// Register embedding generator for rules search (required by GameMechanicsEvaluator)
// Configuration is provided by AppHost via environment variables
builder.Services.AddEmbeddingGenerator(
    builder.Configuration,
    "EmbeddingModel");

// Configure Qdrant client for rules storage (required by RulesSearchService)
builder.Services.AddQdrantClient(builder.Configuration,
    new QdrantExtensions.QdrantConfigurationOptions
    {
        SectionPrefix = "DocumentChunking",
        ConnectionStringName = "qdrant-embeddings",
        RequireConfiguration = false, // Allow fallback to localhost:6334
        DefaultApiKey = "qdrant"
    });

// Register Agents services (required by evaluators)
builder.Services.AddScoped<IRulesSearchService, RulesSearchService>();

// Configure evaluators
// Note: Individual evaluators are already registered as IEvaluator by AddJaimesServices()
// We only need to configure options for BrevityEvaluator
builder.Services.Configure<BrevityEvaluatorOptions>(builder.Configuration.GetSection("Evaluation:Brevity"));
// Register evaluation service
builder.Services.AddScoped<IMessageEvaluationService, MessageEvaluationService>();

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
builder.Services.AddSingleton<IMessageConsumer<ConversationMessageQueuedMessage>, AssistantMessageConsumer>();

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
        "assistant",
        activitySource);
});

// Configure OpenTelemetry ActivitySource
const string activitySourceName = "Jaimes.Workers.AssistantMessageWorker";
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

// Log registered evaluators for debugging
var registeredEvaluators = host.Services.GetServices<IEvaluator>().ToList();
logger.LogInformation(
    "Registered {Count} evaluators: {EvaluatorNames}",
    registeredEvaluators.Count,
    string.Join(", ", registeredEvaluators.Select(e => e.GetType().Name)));

logger.LogInformation("Starting Assistant Message Worker");

await host.RunAsync();

