using System.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Qdrant.Client;
using RabbitMQ.Client;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.ServiceDefaults;
using MattEland.Jaimes.Workers.DocumentEmbedding.Configuration;
using MattEland.Jaimes.Workers.DocumentEmbedding.Consumers;
using MattEland.Jaimes.Workers.DocumentEmbedding.Services;

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
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
    .AddUserSecrets(typeof(Program).Assembly)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

// Bind configuration
DocumentEmbeddingOptions options = builder.Configuration.GetSection("DocumentEmbedding").Get<DocumentEmbeddingOptions>()
    ?? throw new InvalidOperationException("DocumentEmbedding configuration section is required");

builder.Services.AddSingleton(options);

// Add MongoDB client integration
builder.AddMongoDBClient("documents");

// Configure Qdrant client using centralized extension method
// DocumentEmbedding worker uses "qdrant" as default API key (matching ApiService configuration)
// to ensure authentication works with Qdrant instances that require it.
builder.Services.AddQdrantClient(builder.Configuration, new QdrantExtensions.QdrantConfigurationOptions
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
}, out QdrantExtensions.QdrantConnectionConfig qdrantConfig);

// Configure embedding service
// Get Ollama endpoint and model from Aspire connection strings (for default Ollama provider)
string? explicitEndpoint = builder.Configuration["DocumentEmbedding:OllamaEndpoint"]?.TrimEnd('/');
// Get the embedding model connection string provided by Aspire via .WithReference(embedModel)
string? ollamaConnectionString = builder.Configuration.GetConnectionString("embedModel");

// Parse connection string and use explicit endpoint if configured
(string? ollamaEndpoint, string? ollamaModel) = EmbeddingServiceExtensions.ParseOllamaConnectionString(ollamaConnectionString);
ollamaEndpoint = explicitEndpoint ?? ollamaEndpoint;

// Register embedding generator (supports Ollama, Azure OpenAI, and OpenAI)
// Uses Microsoft.Extensions.AI's IEmbeddingGenerator<string, Embedding<float>> interface
// Note: Aspire provides model name via connection string or environment variables

builder.Services.AddEmbeddingGenerator(
    builder.Configuration,
    sectionName: "EmbeddingModel",
    defaultOllamaEndpoint: ollamaEndpoint,
    defaultOllamaModel: ollamaModel);

// Register QdrantClient wrapper
builder.Services.AddSingleton<IQdrantClient>(sp =>
{
    QdrantClient qdrantClient = sp.GetRequiredService<QdrantClient>();
    return new QdrantClientWrapper(qdrantClient);
});

// Register DocumentEmbeddingService
builder.Services.AddSingleton<IDocumentEmbeddingService>(sp =>
{
    IMongoClient mongoClient = sp.GetRequiredService<IMongoClient>();
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator = sp.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
    IQdrantClient qdrantClient = sp.GetRequiredService<IQdrantClient>();
    ILogger<DocumentEmbeddingService> logger = sp.GetRequiredService<ILogger<DocumentEmbeddingService>>();
    ActivitySource activitySource = sp.GetRequiredService<ActivitySource>();
    
    return new DocumentEmbeddingService(
        mongoClient,
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

// Register consumer
builder.Services.AddSingleton<IMessageConsumer<ChunkReadyForEmbeddingMessage>, ChunkReadyForEmbeddingConsumer>();

// Register consumer service (background service)
builder.Services.AddHostedService<MessageConsumerService<ChunkReadyForEmbeddingMessage>>();

// Build host
using IHost host = builder.Build();

ILogger<Program> logger = host.Services.GetRequiredService<ILogger<Program>>();

logger.LogInformation("Starting Document Embedding Worker");
EmbeddingModelOptions embeddingOptions = host.Services.GetRequiredService<EmbeddingModelOptions>();
logger.LogInformation("Embedding Provider: {Provider}", embeddingOptions.Provider);
logger.LogInformation("Embedding Model/Deployment: {Name}", embeddingOptions.Name);
if (!string.IsNullOrWhiteSpace(embeddingOptions.Endpoint))
{
    logger.LogInformation("Embedding Endpoint: {Endpoint}", embeddingOptions.Endpoint);
}
logger.LogInformation("Qdrant: {Host}:{Port} (HTTPS: {UseHttps})", qdrantConfig.Host, qdrantConfig.Port, qdrantConfig.UseHttps);
logger.LogInformation("Worker ready and listening for ChunkReadyForEmbeddingMessage on queue");

await host.RunAsync();

