using System.Diagnostics;
using Microsoft.Extensions.AI;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Services;
using RabbitMQ.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using SemanticChunkerNET;
using MattEland.Jaimes.Workers.DocumentChunking.Consumers;
using MattEland.Jaimes.Workers.DocumentChunking.Configuration;
using MattEland.Jaimes.Workers.DocumentChunking.Services;
using MattEland.Jaimes.ServiceDefaults;
using OllamaSharp;

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
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
    .AddUserSecrets(typeof(Program).Assembly)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

// Bind configuration
DocumentChunkingOptions options = builder.Configuration.GetSection("DocumentChunking").Get<DocumentChunkingOptions>()
    ?? throw new InvalidOperationException("DocumentChunking configuration section is required");

builder.Services.AddSingleton(options);

// Add MongoDB client integration
builder.AddMongoDBClient("documents");

// Configure Qdrant client using centralized extension method
builder.Services.AddQdrantClient(builder.Configuration, new QdrantExtensions.QdrantConfigurationOptions
{
    SectionPrefix = "DocumentChunking",
    ConnectionStringName = "qdrant-embeddings",
    RequireConfiguration = true,
    DefaultApiKey = null // Don't default to "qdrant"
});

// Get Qdrant configuration for logging
string? qdrantHost = builder.Configuration["DocumentChunking:QdrantHost"];
string? qdrantPortStr = builder.Configuration["DocumentChunking:QdrantPort"];
string? qdrantConnectionString = builder.Configuration.GetConnectionString("qdrant-embeddings");

// Extract from connection string if provided (takes precedence)
string? dummyApiKey = null;
if (!string.IsNullOrWhiteSpace(qdrantConnectionString))
{
    QdrantConnectionStringParser.ApplyQdrantConnectionString(qdrantConnectionString, ref qdrantHost, ref qdrantPortStr, ref dummyApiKey);
}

qdrantHost ??= "localhost";
qdrantPortStr ??= "6334";
int.TryParse(qdrantPortStr, out int qdrantPort);
bool useHttps = builder.Configuration.GetValue<bool>("DocumentChunking:QdrantUseHttps", defaultValue: false);

// Configure Ollama client
string? ollamaEndpoint = builder.Configuration["DocumentChunking:OllamaEndpoint"]?.TrimEnd('/');
string? ollamaConnectionString = builder.Configuration.GetConnectionString("nomic-embed-text")
    ?? builder.Configuration.GetConnectionString("ollama-models");

// Parse connection string if endpoint not explicitly set
if (string.IsNullOrWhiteSpace(ollamaEndpoint) && !string.IsNullOrWhiteSpace(ollamaConnectionString))
{
    if (ollamaConnectionString.Contains("Endpoint=", StringComparison.OrdinalIgnoreCase))
    {
        string[] parts = ollamaConnectionString.Split(';');
        ollamaEndpoint = parts.FirstOrDefault(p => p.StartsWith("Endpoint=", StringComparison.OrdinalIgnoreCase))
            ?.Substring("Endpoint=".Length)
            ?.TrimEnd('/');
    }
    else
    {
        ollamaEndpoint = ollamaConnectionString.TrimEnd('/');
    }
}

if (string.IsNullOrWhiteSpace(ollamaEndpoint))
{
    throw new InvalidOperationException(
        "Ollama endpoint is not configured. " +
        "Expected 'DocumentChunking:OllamaEndpoint' from AppHost or connection string from model reference.");
}

string ollamaModel = options.OllamaModel ?? "nomic-embed-text";

// Register IMessagePublisher for queuing chunks without embeddings
builder.Services.AddSingleton<IMessagePublisher, MessagePublisher>();

// Register chunking strategy based on configuration
string chunkingStrategy = options.ChunkingStrategy ?? "SemanticChunker";
bool useSemanticChunker = string.Equals(chunkingStrategy, "SemanticChunker", StringComparison.OrdinalIgnoreCase);
bool useSemanticSlicer = string.Equals(chunkingStrategy, "SemanticSlicer", StringComparison.OrdinalIgnoreCase);

if (useSemanticChunker)
{
    // Register OllamaApiClient from OllamaSharp as the embedding generator
    // OllamaApiClient implements IEmbeddingGenerator<string, Embedding<float>>
    builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
    {
        Uri ollamaUri = new(ollamaEndpoint);
        OllamaApiClient client = new(ollamaUri, ollamaModel);
        return client;
    });

    // Register SemanticChunker and chunking strategy
    builder.Services.AddSingleton<SemanticChunker>(sp =>
    {
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator = 
            sp.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        
        BreakpointThresholdType thresholdType = Enum.Parse<BreakpointThresholdType>(options.ThresholdType, ignoreCase: true);
        
        // Create SemanticChunker with all available configuration options
        // Note: SemanticChunker.NET may not support all parameters - adjust based on actual API
        SemanticChunker chunker = new(
            embeddingGenerator, 
            tokenLimit: options.TokenLimit, 
            thresholdType: thresholdType,
            bufferSize: options.BufferSize,
            thresholdAmount: options.ThresholdAmount,
            targetChunkCount: options.TargetChunkCount);
        
        return chunker;
    });
    builder.Services.AddSingleton<ITextChunkingStrategy, SemanticChunkerStrategy>();
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

// Register consumer
builder.Services.AddSingleton<IMessageConsumer<DocumentReadyForChunkingMessage>, DocumentReadyForChunkingConsumer>();

// Register consumer service (background service)
builder.Services.AddHostedService<MessageConsumerService<DocumentReadyForChunkingMessage>>();

// Build host
using IHost host = builder.Build();

ILogger<Program> logger = host.Services.GetRequiredService<ILogger<Program>>();
IQdrantEmbeddingStore qdrantStore = host.Services.GetRequiredService<IQdrantEmbeddingStore>();

logger.LogInformation("Starting Document Chunking and Embedding Worker");
logger.LogInformation("Chunking Strategy: {Strategy}", chunkingStrategy);
if (useSemanticChunker)
{
    logger.LogInformation("Ollama Endpoint: {Endpoint}", ollamaEndpoint);
    logger.LogInformation("Ollama Model: {Model}", ollamaModel);
}
logger.LogInformation("Qdrant: {Host}:{Port} (HTTPS: {UseHttps})", qdrantHost, qdrantPort, useHttps);
logger.LogInformation("Worker ready and listening for DocumentReadyForChunkingMessage on queue");

// Ensure Qdrant collection exists on startup
try
{
    await qdrantStore.EnsureCollectionExistsAsync();
    logger.LogInformation("Qdrant collection verified/created successfully");
}
catch (Exception ex)
{
    logger.LogWarning(ex, 
        "Failed to ensure Qdrant collection exists on startup (gRPC might not be fully ready yet). " +
        "Collection will be created when processing the first document.");
}

await host.RunAsync();

