HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Add service defaults (telemetry, health checks, service discovery)
// This includes ConfigureOpenTelemetry() AND AddOpenTelemetryExporters() for OTLP export
builder.AddServiceDefaults();


// Reduce HTTP client logging verbosity
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
builder.Logging.AddFilter("System.Net.Http.HttpClient.Default.LogicalHandler", LogLevel.Warning);
builder.Logging.AddFilter("System.Net.Http.HttpClient.Default.ClientHandler", LogLevel.Warning);

// Load configuration
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", false, false)
    .AddJsonFile("appsettings.Development.json", true, false)
    .AddUserSecrets(typeof(Program).Assembly)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

// Bind configuration
ConversationEmbeddingOptions options = builder.Configuration.GetSection("ConversationEmbedding").Get<ConversationEmbeddingOptions>()
                                   ?? throw new InvalidOperationException(
                                       "ConversationEmbedding configuration section is required");

builder.Services.AddSingleton(options);

// Add PostgreSQL with EF Core
builder.Services.AddJaimesRepositories(builder.Configuration);

// Configure Qdrant client using centralized extension method
// ConversationEmbedding worker uses "qdrant" as default API key (matching ApiService configuration)
// to ensure authentication works with Qdrant instances that require it.
builder.Services.AddQdrantClient(builder.Configuration,
    new QdrantExtensions.QdrantConfigurationOptions
    {
        SectionPrefix = "ConversationEmbedding",
        ConnectionStringName = "qdrant-embeddings",
        RequireConfiguration = true,
        DefaultApiKey = "qdrant", // Default to "qdrant" API key for authentication
        AdditionalApiKeyKeys = new[]
        {
            "ConversationEmbedding:QdrantApiKey",
            "ConversationEmbedding__QdrantApiKey"
        }
    },
    out QdrantExtensions.QdrantConnectionConfig qdrantConfig);

// Configure embedding service
// Configuration is provided by AppHost via environment variables
builder.Services.AddEmbeddingGenerator(
    builder.Configuration,
    "EmbeddingModel");

// Register QdrantClient wrapper
builder.Services.AddSingleton<IJaimesEmbeddingClient>(sp =>
{
    QdrantClient qdrantClient = sp.GetRequiredService<QdrantClient>();
    return new QdrantClientWrapper(qdrantClient);
});

// Register ConversationEmbeddingService
builder.Services.AddSingleton<IConversationEmbeddingService>(sp =>
{
    IDbContextFactory<JaimesDbContext> dbContextFactory = sp.GetRequiredService<IDbContextFactory<JaimesDbContext>>();
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator =
        sp.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
    IJaimesEmbeddingClient qdrantClient = sp.GetRequiredService<IJaimesEmbeddingClient>();
    ILogger<ConversationEmbeddingService> logger = sp.GetRequiredService<ILogger<ConversationEmbeddingService>>();
    ActivitySource activitySource = sp.GetRequiredService<ActivitySource>();

    return new ConversationEmbeddingService(
        dbContextFactory,
        embeddingGenerator,
        options,
        qdrantClient,
        logger,
        activitySource);
});

// Configure OpenTelemetry ActivitySource
const string activitySourceName = "Jaimes.Workers.ConversationEmbedding";
ActivitySource activitySource = new(activitySourceName);



// Register ActivitySource for dependency injection
builder.Services.AddSingleton(activitySource);

// Configure message consuming using RabbitMQ.Client (LavinMQ compatible)
IConnectionFactory connectionFactory = RabbitMqConnectionFactory.CreateConnectionFactory(builder.Configuration);
builder.Services.AddSingleton(connectionFactory);

// Register consumer
builder.Services.AddSingleton<IMessageConsumer<ConversationMessageReadyForEmbeddingMessage>, ConversationMessageEmbeddingConsumer>();

// Register consumer service (background service)
builder.Services.AddHostedService<MessageConsumerService<ConversationMessageReadyForEmbeddingMessage>>();

// Build host
using IHost host = builder.Build();

ILogger<Program> logger = host.Services.GetRequiredService<ILogger<Program>>();

logger.LogInformation("Starting Conversation Embedding Worker");
EmbeddingModelOptions embeddingOptions = host.Services.GetRequiredService<EmbeddingModelOptions>();
logger.LogInformation("Embedding Provider: {Provider}", embeddingOptions.Provider);
logger.LogInformation("Embedding Model/Deployment: {Name}", embeddingOptions.Name);
if (!string.IsNullOrWhiteSpace(embeddingOptions.Endpoint))
    logger.LogInformation("Embedding Endpoint: {Endpoint}", embeddingOptions.Endpoint);
logger.LogInformation("Qdrant: {Host}:{Port} (HTTPS: {UseHttps})",
    qdrantConfig.Host,
    qdrantConfig.Port,
    qdrantConfig.UseHttps);
logger.LogInformation("Worker ready and listening for ConversationMessageReadyForEmbeddingMessage on queue");

await host.RunAsync();

