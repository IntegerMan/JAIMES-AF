using System.Diagnostics;
using System.Globalization;
using MassTransit;
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
using MattEland.Jaimes.Workers.DocumentEmbeddings.Consumers;
using MattEland.Jaimes.Workers.DocumentEmbeddings.Configuration;
using MattEland.Jaimes.Workers.DocumentEmbeddings.Services;
using MattEland.Jaimes.ServiceDefaults;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Add Seq endpoint for advanced log monitoring
builder.AddSeqEndpoint("seq");

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

// Load configuration
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
    .AddUserSecrets(typeof(Program).Assembly)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

// Bind configuration
EmbeddingWorkerOptions options = builder.Configuration.GetSection("EmbeddingWorker").Get<EmbeddingWorkerOptions>()
    ?? throw new InvalidOperationException("EmbeddingWorker configuration section is required");

builder.Services.AddSingleton(options);

// Add MongoDB client integration
builder.AddMongoDBClient("documents");

// Configure Qdrant client
// Get Qdrant endpoint from Aspire environment variables
string? qdrantHost = builder.Configuration["EmbeddingWorker:QdrantHost"]
    ?? builder.Configuration["EmbeddingWorker__QdrantHost"];

string? qdrantPortStr = builder.Configuration["EmbeddingWorker:QdrantPort"]
    ?? builder.Configuration["EmbeddingWorker__QdrantPort"];

string? qdrantConnectionString = builder.Configuration.GetConnectionString("qdrant-embeddings")
    ?? builder.Configuration["ConnectionStrings:qdrant-embeddings"]
    ?? builder.Configuration["ConnectionStrings__qdrant-embeddings"];

string? qdrantApiKey = builder.Configuration["Qdrant__ApiKey"]
    ?? builder.Configuration["Qdrant:ApiKey"];

if ((string.IsNullOrWhiteSpace(qdrantHost) || string.IsNullOrWhiteSpace(qdrantPortStr) || string.IsNullOrWhiteSpace(qdrantApiKey))
    && !string.IsNullOrWhiteSpace(qdrantConnectionString))
{
    ApplyQdrantConnectionString(qdrantConnectionString, ref qdrantHost, ref qdrantPortStr, ref qdrantApiKey);
}

if (string.IsNullOrWhiteSpace(qdrantHost) || string.IsNullOrWhiteSpace(qdrantPortStr))
{
    throw new InvalidOperationException(
        "Qdrant host and port are not configured. " +
        "Expected 'EmbeddingWorker:QdrantHost' and 'EmbeddingWorker:QdrantPort' from Aspire.");
}

if (!int.TryParse(qdrantPortStr, out int qdrantPort))
{
    throw new InvalidOperationException(
        $"Invalid Qdrant port: '{qdrantPortStr}'. Expected a valid integer.");
}

// QdrantClient uses the gRPC endpoint (container port 6334, though Aspire may map it to another host port)
// Port 6333 is for HTTP REST API, port 6334 is for gRPC
// For local development, Qdrant typically doesn't use HTTPS, so use http: false
// In production with proper TLS setup, this should be set to true
bool useHttps = builder.Configuration.GetValue<bool>("EmbeddingWorker:QdrantUseHttps", defaultValue: false);

QdrantClient qdrantClient = string.IsNullOrWhiteSpace(qdrantApiKey)
    ? new QdrantClient(qdrantHost, port: qdrantPort, https: useHttps)
    : new QdrantClient(qdrantHost, port: qdrantPort, https: useHttps, apiKey: qdrantApiKey);

builder.Services.AddSingleton(qdrantClient);

// Configure Ollama client using connection string from Aspire
// When referencing an OllamaModelResource, Aspire provides a connection string
// Try multiple possible connection string names that Aspire might use
string? ollamaConnectionString = builder.Configuration.GetConnectionString("nomic-embed-text")
    ?? builder.Configuration.GetConnectionString("ollama-models")
    ?? builder.Configuration.GetConnectionString("embedModel")
    ?? builder.Configuration["ConnectionStrings:nomic-embed-text"]
    ?? builder.Configuration["ConnectionStrings:ollama-models"]
    ?? builder.Configuration["ConnectionStrings:embedModel"]
    ?? builder.Configuration["ConnectionStrings__nomic-embed-text"]
    ?? builder.Configuration["ConnectionStrings__ollama-models"]
    ?? builder.Configuration["ConnectionStrings__embedModel"];

if (string.IsNullOrWhiteSpace(ollamaConnectionString))
{
    // Fallback: Try to construct from Ollama endpoint if available
    // This handles cases where the connection string isn't provided but we have the endpoint
    string? ollamaEndpointFromConfig = builder.Configuration["EmbeddingWorker:OllamaEndpoint"]
        ?? builder.Configuration["EmbeddingWorker__OllamaEndpoint"];
    
    if (!string.IsNullOrWhiteSpace(ollamaEndpointFromConfig))
    {
        // Construct connection string from endpoint
        ollamaConnectionString = ollamaEndpointFromConfig.TrimEnd('/');
    }
    else
    {
        // Last resort: try to get from Ollama resource connection string
        string? ollamaResourceConnectionString = builder.Configuration.GetConnectionString("ollama-models")
            ?? builder.Configuration["ConnectionStrings:ollama-models"]
            ?? builder.Configuration["ConnectionStrings__ollama-models"];
        
        if (!string.IsNullOrWhiteSpace(ollamaResourceConnectionString))
        {
            ollamaConnectionString = ollamaResourceConnectionString;
        }
    }
}

if (string.IsNullOrWhiteSpace(ollamaConnectionString))
{
    throw new InvalidOperationException(
        "Ollama connection string is not configured. " +
        "Expected connection string from Aspire model reference. " +
        "Tried: 'nomic-embed-text', 'ollama-models', 'embedModel'. " +
        "Check that the embedding worker has a reference to the Ollama model resource in AppHost.");
}

// Parse connection string (format: Endpoint=http://host:port;Model=model-name or just http://host:port)
string ollamaEndpoint;
string ollamaModel = options.OllamaModel ?? "nomic-embed-text";

if (ollamaConnectionString.Contains("Endpoint=", StringComparison.OrdinalIgnoreCase))
{
    // Parse connection string format: Endpoint=http://host:port;Model=model-name
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
    // Assume it's just the endpoint URL
    ollamaEndpoint = ollamaConnectionString;
}

// Normalize endpoint (remove trailing slash)
ollamaEndpoint = ollamaEndpoint.TrimEnd('/');

builder.Services.AddSingleton(new OllamaEmbeddingOptions
{
    Endpoint = ollamaEndpoint,
    Model = ollamaModel
});

// Register HttpClient for Ollama API calls with extended timeout and resilience
// Embedding generation can take a long time for large documents, so we need:
// 1. A longer timeout (configurable, default 15 minutes)
// 2. Retry logic for transient errors (socket exceptions, timeouts)
// 3. Allow POST retries since embedding generation is idempotent
TimeSpan httpClientTimeout = TimeSpan.FromMinutes(options.HttpClientTimeoutMinutes);
builder.Services.AddHttpClient<IOllamaEmbeddingService, OllamaEmbeddingService>(client =>
{
    client.BaseAddress = new Uri(ollamaEndpoint);
    client.Timeout = httpClientTimeout;
})
.AddStandardResilienceHandler(resilienceOptions =>
{
    // Configure a longer total request timeout (slightly longer than HttpClient timeout)
    resilienceOptions.TotalRequestTimeout.Timeout = httpClientTimeout.Add(TimeSpan.FromMinutes(1));
    
    // Configure retry policy for transient errors
    // Allow POST retries since embedding generation is idempotent
    resilienceOptions.Retry.ShouldHandle = args =>
    {
        // Retry on socket exceptions (connection aborted, connection refused, etc.)
        if (args.Outcome.Exception != null)
        {
            // Check for socket exceptions and timeout exceptions (including inner exceptions)
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
        
        // Retry on HTTP error status codes
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

// Register services
builder.Services.AddSingleton<IOllamaEmbeddingService, OllamaEmbeddingService>();
builder.Services.AddSingleton<IDocumentEmbeddingService, DocumentEmbeddingService>();
builder.Services.AddSingleton<IQdrantEmbeddingStore, QdrantEmbeddingStore>();

// Configure MassTransit
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<DocumentCrackedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        // Get RabbitMQ connection string from Aspire
        string? connectionString = builder.Configuration.GetConnectionString("messaging")
            ?? builder.Configuration["ConnectionStrings:messaging"]
            ?? builder.Configuration["ConnectionStrings__messaging"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "RabbitMQ connection string is not configured. " +
                "Expected connection string 'messaging' from Aspire.");
        }

        // Parse connection string (format: amqp://username:password@host:port/vhost)
        Uri rabbitUri = new(connectionString);
        string host = rabbitUri.Host;
        ushort port = rabbitUri.Port > 0 ? (ushort)rabbitUri.Port : (ushort)5672;
        string? username = null;
        string? password = null;
        
        if (!string.IsNullOrEmpty(rabbitUri.UserInfo))
        {
            string[] userInfo = rabbitUri.UserInfo.Split(':');
            username = userInfo[0];
            if (userInfo.Length > 1)
            {
                password = userInfo[1];
            }
        }

        cfg.Host(host, port, "/", h =>
        {
            if (!string.IsNullOrEmpty(username))
            {
                h.Username(username);
            }
            if (!string.IsNullOrEmpty(password))
            {
                h.Password(password);
            }
        });

        // Configure retry policy
        cfg.UseMessageRetry(r => r.Exponential(
            retryLimit: 5,
            minInterval: TimeSpan.FromSeconds(1),
            maxInterval: TimeSpan.FromSeconds(30),
            intervalDelta: TimeSpan.FromSeconds(2)));

        // Configure consumer endpoint
        // MassTransit will automatically create the queue and bind to the appropriate exchange
        // based on the message type (DocumentCrackedMessage)
        cfg.ConfigureEndpoints(context);
    });
});

// Configure OpenTelemetry ActivitySource
const string activitySourceName = "Jaimes.Workers.DocumentEmbeddings";
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
IQdrantEmbeddingStore qdrantStore = host.Services.GetRequiredService<IQdrantEmbeddingStore>();

logger.LogInformation("Starting Document Embeddings Worker");
logger.LogInformation("Qdrant: {Host}:{Port} (HTTPS: {UseHttps})", qdrantHost, qdrantPort, useHttps);
logger.LogInformation("Ollama Model: {Model}", options.OllamaModel ?? "nomic-embed-text");

// Ensure Qdrant collection exists on startup
// Note: WaitFor(qdrant) should ensure Qdrant is ready, but gRPC service might need a moment
// to fully initialize after the HTTP health check passes
try
{
    await qdrantStore.EnsureCollectionExistsAsync();
    logger.LogInformation("Qdrant collection verified/created successfully");
}
catch (Exception ex)
{
    // If WaitFor passed but gRPC isn't ready yet, log and continue
    // Collection will be created when first document is processed
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

