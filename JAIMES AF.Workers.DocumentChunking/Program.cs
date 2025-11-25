using System.Diagnostics;
using System.Globalization;
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

// Configure Qdrant client
// Get Qdrant endpoint from Aspire environment variables
string? qdrantHost = builder.Configuration["DocumentChunking:QdrantHost"]
    ?? builder.Configuration["DocumentChunking__QdrantHost"]
    ?? builder.Configuration["QDRANT_EMBEDDINGS_GRPCHOST"]
    ?? builder.Configuration["QdrantEmbeddings__GrpcHost"];

string? qdrantPortStr = builder.Configuration["DocumentChunking:QdrantPort"]
    ?? builder.Configuration["DocumentChunking__QdrantPort"]
    ?? builder.Configuration["QDRANT_EMBEDDINGS_GRPCPORT"]
    ?? builder.Configuration["QdrantEmbeddings__GrpcPort"];

string? qdrantConnectionString = builder.Configuration.GetConnectionString("qdrant-embeddings")
    ?? builder.Configuration["ConnectionStrings:qdrant-embeddings"]
    ?? builder.Configuration["ConnectionStrings__qdrant-embeddings"];

// Initialize API key variable
string? qdrantApiKey = null;

// Try to extract API key from connection string first (this is the most reliable source)
if (!string.IsNullOrWhiteSpace(qdrantConnectionString))
{
    ApplyQdrantConnectionString(qdrantConnectionString, ref qdrantHost, ref qdrantPortStr, ref qdrantApiKey);
}

// Try multiple possible API key locations
qdrantApiKey ??= builder.Configuration["Qdrant__ApiKey"]
    ?? builder.Configuration["Qdrant:ApiKey"]
    ?? builder.Configuration["QDRANT_EMBEDDINGS_APIKEY"]
    ?? builder.Configuration["QdrantEmbeddings__ApiKey"]
    ?? builder.Configuration["QDRANT_EMBEDDINGS_API_KEY"]
    ?? Environment.GetEnvironmentVariable("Qdrant__ApiKey")
    ?? Environment.GetEnvironmentVariable("QDRANT_EMBEDDINGS_APIKEY")
    ?? Environment.GetEnvironmentVariable("QdrantEmbeddings__ApiKey")
    ?? Environment.GetEnvironmentVariable("QDRANT_EMBEDDINGS_API_KEY")
    ?? Environment.GetEnvironmentVariable("qdrant-api-key");

// If the API key looks like an unresolved Aspire expression (contains { and }), 
// try to resolve it by reading from the actual parameter value or use default
if (!string.IsNullOrWhiteSpace(qdrantApiKey) && qdrantApiKey.Contains('{') && qdrantApiKey.Contains('}'))
{
    string? resolvedApiKey = Environment.GetEnvironmentVariable("qdrant-api-key")
        ?? Environment.GetEnvironmentVariable("QDRANT_API_KEY")
        ?? Environment.GetEnvironmentVariable("Qdrant__ApiKey")
        ?? Environment.GetEnvironmentVariable("QDRANT_EMBEDDINGS_APIKEY");
    
    if (!string.IsNullOrWhiteSpace(resolvedApiKey) && !resolvedApiKey.Contains('{'))
    {
        qdrantApiKey = resolvedApiKey;
    }
    else
    {
        qdrantApiKey = "qdrant";
    }
}

// If API key is still not found, use the default value from the parameter definition
if (string.IsNullOrWhiteSpace(qdrantApiKey))
{
    qdrantApiKey = "qdrant";
}

if (string.IsNullOrWhiteSpace(qdrantHost) || string.IsNullOrWhiteSpace(qdrantPortStr))
{
    throw new InvalidOperationException(
        "Qdrant host and port are not configured. " +
        "Expected 'DocumentChunking:QdrantHost' and 'DocumentChunking:QdrantPort' from Aspire.");
}

if (!int.TryParse(qdrantPortStr, out int qdrantPort))
{
    throw new InvalidOperationException(
        $"Invalid Qdrant port: '{qdrantPortStr}'. Expected a valid integer.");
}

// QdrantClient uses the gRPC endpoint (container port 6334, though Aspire may map it to another host port)
bool useHttps = builder.Configuration.GetValue<bool>("DocumentChunking:QdrantUseHttps", defaultValue: false);

QdrantClient qdrantClient = string.IsNullOrWhiteSpace(qdrantApiKey)
    ? new QdrantClient(qdrantHost, port: qdrantPort, https: useHttps)
    : new QdrantClient(qdrantHost, port: qdrantPort, https: useHttps, apiKey: qdrantApiKey);

builder.Services.AddSingleton(qdrantClient);

// Configure Ollama client - prioritize explicit endpoint configuration from AppHost
// This ensures we use the correct host/port in containerized environments
string? ollamaConnectionString = null;

// First, check for explicit endpoint configuration (set by AppHost)
string? ollamaEndpointFromConfig = builder.Configuration["DocumentChunking:OllamaEndpoint"]
    ?? builder.Configuration["DocumentChunking__OllamaEndpoint"];

if (!string.IsNullOrWhiteSpace(ollamaEndpointFromConfig))
{
    ollamaConnectionString = ollamaEndpointFromConfig.TrimEnd('/');
}
else
{
    // Fallback to connection strings from Aspire model reference
    ollamaConnectionString = builder.Configuration.GetConnectionString("nomic-embed-text")
        ?? builder.Configuration.GetConnectionString("ollama-models")
        ?? builder.Configuration.GetConnectionString("embedModel")
        ?? builder.Configuration["ConnectionStrings:nomic-embed-text"]
        ?? builder.Configuration["ConnectionStrings:ollama-models"]
        ?? builder.Configuration["ConnectionStrings:embedModel"]
        ?? builder.Configuration["ConnectionStrings__nomic-embed-text"]
        ?? builder.Configuration["ConnectionStrings__ollama-models"]
        ?? builder.Configuration["ConnectionStrings__embedModel"];
}

if (string.IsNullOrWhiteSpace(ollamaConnectionString))
{
    throw new InvalidOperationException(
        "Ollama connection string is not configured. " +
        "Expected connection string from Aspire model reference. " +
        "Tried: 'nomic-embed-text', 'ollama-models', 'embedModel'. " +
        "Check that the chunking worker has a reference to the Ollama model resource in AppHost.");
}

// Parse connection string
string ollamaEndpoint;
string ollamaModel = options.OllamaModel ?? "nomic-embed-text";

if (ollamaConnectionString.Contains("Endpoint=", StringComparison.OrdinalIgnoreCase))
{
    string[] parts = ollamaConnectionString.Split(';');
    ollamaEndpoint = parts.FirstOrDefault(p => p.StartsWith("Endpoint=", StringComparison.OrdinalIgnoreCase))
        ?.Substring("Endpoint=".Length)
        ?? throw new InvalidOperationException("Invalid Ollama connection string format: missing Endpoint");
    
    string? modelPart = parts.FirstOrDefault(p => p.StartsWith("Model=", StringComparison.OrdinalIgnoreCase));
    if (!string.IsNullOrWhiteSpace(modelPart))
    {
        ollamaModel = modelPart.Substring("Model=".Length);
    }
}
else
{
    ollamaEndpoint = ollamaConnectionString;
}

ollamaEndpoint = ollamaEndpoint.TrimEnd('/');

builder.Services.AddSingleton(new OllamaEmbeddingOptions
{
    Endpoint = ollamaEndpoint,
    Model = ollamaModel
});

// Register HttpClient for Ollama API calls (used by OllamaEmbeddingGeneratorAdapter)
TimeSpan httpClientTimeout = TimeSpan.FromMinutes(15);
builder.Services.AddHttpClient<OllamaEmbeddingGeneratorAdapter>(client =>
{
    client.BaseAddress = new Uri(ollamaEndpoint);
    client.Timeout = httpClientTimeout;
})
.AddStandardResilienceHandler(resilienceOptions =>
{
    resilienceOptions.TotalRequestTimeout.Timeout = httpClientTimeout.Add(TimeSpan.FromMinutes(1));
    
    resilienceOptions.Retry.ShouldHandle = args =>
    {
        if (args.Outcome.Exception != null)
        {
            Exception? ex = args.Outcome.Exception;
            while (ex != null)
            {
                if (ex is HttpRequestException ||
                    ex is System.Net.Sockets.SocketException ||
                    ex is TaskCanceledException ||
                    ex is TimeoutException)
                {
                    return ValueTask.FromResult(true);
                }
                ex = ex.InnerException;
            }
        }
        
        if (args.Outcome.Result != null)
        {
            return ValueTask.FromResult(
                args.Outcome.Result.StatusCode == System.Net.HttpStatusCode.RequestTimeout ||
                args.Outcome.Result.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable ||
                args.Outcome.Result.StatusCode == System.Net.HttpStatusCode.GatewayTimeout ||
                (args.Outcome.Result.StatusCode >= System.Net.HttpStatusCode.InternalServerError &&
                 args.Outcome.Result.StatusCode <= (System.Net.HttpStatusCode)599));
        }
        
        return ValueTask.FromResult(false);
    };
});

// Register embedding generator adapter for SemanticChunker.NET
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>, OllamaEmbeddingGeneratorAdapter>();

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
logger.LogInformation("Ollama Endpoint: {Endpoint}", ollamaEndpoint);
logger.LogInformation("Ollama Model: {Model}", ollamaModel);
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

static void ApplyQdrantConnectionString(
    string connectionString,
    ref string? host,
    ref string? port,
    ref string? apiKey)
{
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return;
    }

    if (Uri.TryCreate(connectionString, UriKind.Absolute, out Uri? uri))
    {
        host ??= uri.Host;
        if (uri.Port > 0)
        {
            port ??= uri.Port.ToString(CultureInfo.InvariantCulture);
        }

        ExtractApiKeyFromQuery(uri.Query, ref apiKey);
        return;
    }

    if (TryParseHostAndPort(connectionString, out string? parsedHost, out string? parsedPort))
    {
        host ??= parsedHost;
        if (string.IsNullOrWhiteSpace(port) && !string.IsNullOrWhiteSpace(parsedPort))
        {
            port = parsedPort;
        }
    }

    string[] segments = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
    foreach (string segment in segments)
    {
        string[] keyValue = segment.Split('=', 2, StringSplitOptions.TrimEntries);
        if (keyValue.Length != 2)
        {
            continue;
        }

        string key = keyValue[0];
        string value = keyValue[1];

        if (string.Equals(key, "Endpoint", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "Uri", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "GrpcUri", StringComparison.OrdinalIgnoreCase))
        {
            ApplyQdrantConnectionString(value, ref host, ref port, ref apiKey);
            continue;
        }

        if (string.Equals(key, "Host", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "Hostname", StringComparison.OrdinalIgnoreCase))
        {
            host ??= value;
            continue;
        }

        if (string.Equals(key, "Port", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "GrpcPort", StringComparison.OrdinalIgnoreCase))
        {
            port ??= value;
            continue;
        }

        if (string.Equals(key, "ApiKey", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "Api-Key", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "Api_Key", StringComparison.OrdinalIgnoreCase))
        {
            apiKey ??= value;
        }
    }
}

static bool TryParseHostAndPort(string value, out string? host, out string? port)
{
    host = null;
    port = null;

    if (string.IsNullOrWhiteSpace(value) || value.Contains('='))
    {
        return false;
    }

    string[] hostParts = value.Split(':', StringSplitOptions.RemoveEmptyEntries);
    if (hostParts.Length == 0)
    {
        return false;
    }

    host = hostParts[0];
    if (hostParts.Length > 1)
    {
        port = hostParts[1];
    }

    return true;
}

static void ExtractApiKeyFromQuery(string query, ref string? apiKey)
{
    if (string.IsNullOrWhiteSpace(query) || !string.IsNullOrWhiteSpace(apiKey))
    {
        return;
    }

    string trimmedQuery = query.TrimStart('?');
    string[] pairs = trimmedQuery.Split('&', StringSplitOptions.RemoveEmptyEntries);

    foreach (string pair in pairs)
    {
        string[] keyValue = pair.Split('=', 2);
        if (keyValue.Length != 2)
        {
            continue;
        }

        string key = keyValue[0];
        if (string.Equals(key, "api-key", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "apikey", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "api_key", StringComparison.OrdinalIgnoreCase))
        {
            apiKey = Uri.UnescapeDataString(keyValue[1]);
            break;
        }
    }
}

