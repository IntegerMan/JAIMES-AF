using MattEland.Jaimes.Agents.Services;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.ServiceDefaults;
using MattEland.Jaimes.ServiceLayer;
using MattEland.Jaimes.Workers.AssistantMessageWorker.Consumers;
using MattEland.Jaimes.Workers.AssistantMessageWorker.Services;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;
using MattEland.Jaimes.Evaluators;

// Enable experimental OpenTelemetry tracing for Azure OpenAI SDK
// This enables gen_ai.* semantic conventions for rich AI telemetry in Aspire dashboard
AppContext.SetSwitch("OpenAI.Experimental.EnableOpenTelemetry", true);

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

// Register consumers
builder.Services.AddSingleton<IMessageConsumer<ConversationMessageQueuedMessage>, AssistantMessageConsumer>();
builder.Services.AddSingleton<IMessageConsumer<EvaluatorTaskMessage>, EvaluatorTaskConsumer>();

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

// Register evaluator task consumer service (for parallel evaluation processing)
builder.Services.AddHostedService(serviceProvider =>
{
    IConnectionFactory factory = serviceProvider.GetRequiredService<IConnectionFactory>();
    IMessageConsumer<EvaluatorTaskMessage> consumer =
        serviceProvider.GetRequiredService<IMessageConsumer<EvaluatorTaskMessage>>();
    ILogger<MessageConsumerService<EvaluatorTaskMessage>> consumerLogger = serviceProvider
        .GetRequiredService<ILogger<MessageConsumerService<EvaluatorTaskMessage>>>();
    ActivitySource? actSource = serviceProvider.GetService<ActivitySource>();
    return new MessageConsumerService<EvaluatorTaskMessage>(
        factory,
        consumer,
        consumerLogger,
        actSource);
});

// Configure OpenTelemetry ActivitySource for this worker
// Note: AddServiceDefaults() already configured OpenTelemetry with all necessary sources including:
// - "Jaimes.Workers.*" (matches this worker's activity source)
// - "Jaimes.Agents.*" (matches ChatClientMiddleware)
// - "Jaimes.ApiService" (matches WrapWithInstrumentation)
// - "Microsoft.Extensions.AI" (matches UseOpenTelemetry from chat client)
// We only need to register the ActivitySource for DI injection.
const string activitySourceName = "Jaimes.Workers.AssistantMessageWorker";
ActivitySource activitySource = new(activitySourceName);

// Register ActivitySource for dependency injection
builder.Services.AddSingleton(activitySource);

// Build host
using IHost host = builder.Build();

ILogger<Program> logger = host.Services.GetRequiredService<ILogger<Program>>();

await host.WaitForMigrationsAsync();

logger.LogInformation("Starting Assistant Message Worker");

await host.RunAsync();

