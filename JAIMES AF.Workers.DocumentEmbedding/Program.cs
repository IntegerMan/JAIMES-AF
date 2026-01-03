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
DocumentEmbeddingOptions options = builder.Configuration.GetSection("DocumentEmbedding").Get<DocumentEmbeddingOptions>()
                                   ?? throw new InvalidOperationException(
                                       "DocumentEmbedding configuration section is required");

builder.Services.AddSingleton(options);

// Add PostgreSQL with EF Core
builder.Services.AddJaimesRepositories(builder.Configuration);

// Configure Qdrant client using centralized extension method
// DocumentEmbedding worker uses "qdrant" as default API key (matching ApiService configuration)
// to ensure authentication works with Qdrant instances that require it.
builder.Services.AddQdrantClient(builder.Configuration,
    new QdrantExtensions.QdrantConfigurationOptions
    {
        SectionPrefix = "DocumentEmbedding",
        ConnectionStringName = "qdrant-embeddings",
        RequireConfiguration = true,
        DefaultApiKey = "qdrant", // Default to "qdrant" API key for authentication
        AdditionalApiKeyKeys = new[]
        {
            "DocumentEmbedding:QdrantApiKey",
            "DocumentEmbedding__QdrantApiKey",
            "DocumentChunking:QdrantApiKey",
            "DocumentChunking__QdrantApiKey"
        }
    },
    out QdrantExtensions.QdrantConnectionConfig qdrantConfig);

// Configure embedding service
// Configuration is provided by AppHost via environment variables
// Legacy DocumentEmbedding:OllamaEndpoint is supported for backward compatibility
builder.Services.AddEmbeddingGenerator(
    builder.Configuration,
    "EmbeddingModel");

// Register QdrantClient wrapper
builder.Services.AddSingleton<IJaimesEmbeddingClient>(sp =>
{
    QdrantClient qdrantClient = sp.GetRequiredService<QdrantClient>();
    return new QdrantClientWrapper(qdrantClient);
});

// Register DocumentEmbeddingService
builder.Services.AddSingleton<IDocumentEmbeddingService>(sp =>
{
    IDbContextFactory<JaimesDbContext> dbContextFactory = sp.GetRequiredService<IDbContextFactory<JaimesDbContext>>();
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator =
        sp.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
    IJaimesEmbeddingClient qdrantClient = sp.GetRequiredService<IJaimesEmbeddingClient>();
    ILogger<DocumentEmbeddingService> logger = sp.GetRequiredService<ILogger<DocumentEmbeddingService>>();
    ActivitySource activitySource = sp.GetRequiredService<ActivitySource>();

    return new DocumentEmbeddingService(
        dbContextFactory,
        embeddingGenerator,
        options,
        qdrantClient,
        logger,
        activitySource);
});

// Configure OpenTelemetry ActivitySource
const string activitySourceName = "Jaimes.Workers.DocumentEmbedding";
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

// Configure message consuming using RabbitMQ.Client (LavinMQ compatible)
IConnectionFactory connectionFactory = RabbitMqConnectionFactory.CreateConnectionFactory(builder.Configuration);
builder.Services.AddSingleton(connectionFactory);

// Configure HTTP client for API service communication (pipeline status reporting)
string? apiBaseUrl = builder.Configuration["Services:apiservice:http:0"]
    ?? builder.Configuration["Services:apiservice:https:0"]
    ?? builder.Configuration["ApiService:BaseUrl"];

// Register pipeline status reporter if API service is configured
if (!string.IsNullOrEmpty(apiBaseUrl))
{
    builder.Services.AddHttpClient("ApiService", client =>
    {
        client.BaseAddress = new Uri(apiBaseUrl);
        client.Timeout = TimeSpan.FromSeconds(5);
    });

    builder.Services.AddSingleton<IPipelineStatusReporter>(sp =>
    {
        IHttpClientFactory httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
        HttpClient httpClient = httpClientFactory.CreateClient("ApiService");
        ILogger<HttpPipelineStatusReporter> reporterLogger = sp.GetRequiredService<ILogger<HttpPipelineStatusReporter>>();
        return new HttpPipelineStatusReporter(httpClient, reporterLogger, "DocumentEmbeddingWorker");
    });
}

// Register consumer
builder.Services.AddSingleton<IMessageConsumer<ChunkReadyForEmbeddingMessage>, ChunkReadyForEmbeddingConsumer>();

// Register consumer service (background service) with pipeline status reporting
builder.Services.AddHostedService(sp =>
{
    IConnectionFactory connFactory = sp.GetRequiredService<IConnectionFactory>();
    IMessageConsumer<ChunkReadyForEmbeddingMessage> consumer = sp.GetRequiredService<IMessageConsumer<ChunkReadyForEmbeddingMessage>>();
    ILogger<MessageConsumerService<ChunkReadyForEmbeddingMessage>> consumerLogger = sp.GetRequiredService<ILogger<MessageConsumerService<ChunkReadyForEmbeddingMessage>>>();
    ActivitySource? activity = sp.GetService<ActivitySource>();
    IPipelineStatusReporter? statusReporter = sp.GetService<IPipelineStatusReporter>();
    
    return new MessageConsumerService<ChunkReadyForEmbeddingMessage>(
        connFactory, consumer, consumerLogger, activity, statusReporter, "embedding");
});

// Build host
using IHost host = builder.Build();

ILogger<Program> logger = host.Services.GetRequiredService<ILogger<Program>>();

logger.LogInformation("Starting Document Embedding Worker");
EmbeddingModelOptions embeddingOptions = host.Services.GetRequiredService<EmbeddingModelOptions>();
logger.LogInformation("Embedding Provider: {Provider}", embeddingOptions.Provider);
logger.LogInformation("Embedding Model/Deployment: {Name}", embeddingOptions.Name);
if (!string.IsNullOrWhiteSpace(embeddingOptions.Endpoint))
    logger.LogInformation("Embedding Endpoint: {Endpoint}", embeddingOptions.Endpoint);
logger.LogInformation("Qdrant: {Host}:{Port} (HTTPS: {UseHttps})",
    qdrantConfig.Host,
    qdrantConfig.Port,
    qdrantConfig.UseHttps);
logger.LogInformation("Worker ready and listening for ChunkReadyForEmbeddingMessage on queue");

await host.RunAsync();