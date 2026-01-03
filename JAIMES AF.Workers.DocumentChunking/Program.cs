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

// Reduce HTTP client logging verbosity - filter out embedding request logs
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
DocumentChunkingOptions options = builder.Configuration.GetSection("DocumentChunking").Get<DocumentChunkingOptions>()
                                  ?? throw new InvalidOperationException(
                                      "DocumentChunking configuration section is required");

builder.Services.AddSingleton(options);

// Add PostgreSQL with EF Core
builder.Services.AddJaimesRepositories(builder.Configuration);

// Configure Qdrant client using centralized extension method
builder.Services.AddQdrantClient(builder.Configuration,
    new QdrantExtensions.QdrantConfigurationOptions
    {
        SectionPrefix = "DocumentChunking",
        ConnectionStringName = "qdrant-embeddings",
        RequireConfiguration = true,
        DefaultApiKey = null // Don't default to "qdrant"
    },
    out QdrantExtensions.QdrantConnectionConfig qdrantConfig);

// Register IMessagePublisher for queuing chunks without embeddings
builder.Services.AddSingleton<IMessagePublisher, MessagePublisher>();

// Register chunking strategy based on configuration
string chunkingStrategy = options.ChunkingStrategy ?? "SemanticChunker";
bool useSemanticChunker = string.Equals(chunkingStrategy, "SemanticChunker", StringComparison.OrdinalIgnoreCase);
bool useSemanticSlicer = string.Equals(chunkingStrategy, "SemanticSlicer", StringComparison.OrdinalIgnoreCase);

// For logging later
string? logOllamaEndpoint = null;
string? logOllamaModel = null;

if (useSemanticChunker)
{
    // Configure embedding service
    // Configuration is provided by AppHost via environment variables
    // Legacy DocumentChunking:OllamaEndpoint is supported for backward compatibility
    builder.Services.AddEmbeddingGenerator(
        builder.Configuration,
        "EmbeddingModel");

    // Register SemanticChunker and chunking strategy
    builder.Services.AddSingleton<SemanticChunker>(sp =>
    {
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator =
            sp.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>(
            );

        BreakpointThresholdType thresholdType = Enum.Parse<BreakpointThresholdType>(options.ThresholdType, true);

        // Create SemanticChunker with all available configuration options
        // Note: SemanticChunker.NET may not support all parameters - adjust based on actual API
        SemanticChunker chunker = new(
            embeddingGenerator,
            options.TokenLimit,
            thresholdType: thresholdType,
            bufferSize: options.BufferSize,
            thresholdAmount: options.ThresholdAmount,
            targetChunkCount: options.TargetChunkCount);

        return chunker;
    });
    builder.Services.AddSingleton<ITextChunkingStrategy, SemanticChunkerStrategy>();

    // Capture for logging (read from configuration)
    logOllamaEndpoint = builder.Configuration["EmbeddingModel:Endpoint"];
    logOllamaModel = builder.Configuration["EmbeddingModel:Name"];
}
else if (useSemanticSlicer)
{
    // Register SemanticSlicer strategy (does not require embedding model)
    builder.Services.AddSingleton<ITextChunkingStrategy, SemanticSlicerStrategy>();
}
else
{
    throw new InvalidOperationException(
        $"Invalid chunking strategy '{chunkingStrategy}'. " +
        "Valid options are 'SemanticChunker' or 'SemanticSlicer'.");
}

// Register services
builder.Services.AddSingleton<IQdrantEmbeddingStore, QdrantEmbeddingStore>();
builder.Services.AddSingleton<IDocumentChunkingService, DocumentChunkingService>();

// Configure OpenTelemetry ActivitySource (register before MessageConsumerService to ensure it's available for injection)
const string activitySourceName = "Jaimes.Workers.DocumentChunking";
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

// Register ActivitySource for dependency injection (before MessageConsumerService)
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
        return new HttpPipelineStatusReporter(httpClient, reporterLogger, "DocumentChunkingWorker");
    });
}

// Register consumer
builder.Services.AddSingleton<IMessageConsumer<DocumentReadyForChunkingMessage>, DocumentReadyForChunkingConsumer>();

// Register consumer service (background service) with pipeline status reporting
builder.Services.AddHostedService(sp =>
{
    IConnectionFactory connFactory = sp.GetRequiredService<IConnectionFactory>();
    IMessageConsumer<DocumentReadyForChunkingMessage> consumer = sp.GetRequiredService<IMessageConsumer<DocumentReadyForChunkingMessage>>();
    ILogger<MessageConsumerService<DocumentReadyForChunkingMessage>> consumerLogger = sp.GetRequiredService<ILogger<MessageConsumerService<DocumentReadyForChunkingMessage>>>();
    ActivitySource? activity = sp.GetService<ActivitySource>();
    IPipelineStatusReporter? statusReporter = sp.GetService<IPipelineStatusReporter>();
    
    return new MessageConsumerService<DocumentReadyForChunkingMessage>(
        connFactory, consumer, consumerLogger, activity, statusReporter, "chunking");
});

// Build host
using IHost host = builder.Build();

ILogger<Program> logger = host.Services.GetRequiredService<ILogger<Program>>();
IQdrantEmbeddingStore qdrantStore = host.Services.GetRequiredService<IQdrantEmbeddingStore>();

await host.WaitForMigrationsAsync();

logger.LogInformation("Starting Document Chunking and Embedding Worker");
logger.LogInformation("Chunking Strategy: {Strategy}", chunkingStrategy);
if (useSemanticChunker)
{
    logger.LogInformation("Ollama Endpoint: {Endpoint}", logOllamaEndpoint);
    if (!string.IsNullOrWhiteSpace(logOllamaModel)) logger.LogInformation("Ollama Model: {Model}", logOllamaModel);
}

logger.LogInformation("Qdrant: {Host}:{Port} (HTTPS: {UseHttps})",
    qdrantConfig.Host,
    qdrantConfig.Port,
    qdrantConfig.UseHttps);
logger.LogInformation("Worker ready and listening for DocumentReadyForChunkingMessage on queue");

// Ensure Qdrant collection exists on startup
try
{
    // Only attempt creation if we have an embedding generator (SemanticChunker). Otherwise skip and let embedding worker create it.
    if (useSemanticChunker)
    {
        await qdrantStore.EnsureCollectionExistsAsync();
        logger.LogInformation("Qdrant collection verified/created successfully");
    }
    else
    {
        logger.LogInformation(
            "Skipping Qdrant collection creation on startup: no embedding generator is registered. It will be created when embeddings are generated.");
    }
}
catch (Exception ex)
{
    logger.LogWarning(ex,
        "Failed to ensure Qdrant collection exists on startup (gRPC might not be fully ready yet). " +
        "Collection will be created when processing the first document.");
}

await host.RunAsync();