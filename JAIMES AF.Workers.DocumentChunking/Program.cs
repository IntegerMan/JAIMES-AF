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

// Register HttpClient for Ollama API calls
TimeSpan httpClientTimeout = TimeSpan.FromMinutes(15);
builder.Services.AddHttpClient<IOllamaEmbeddingService, OllamaEmbeddingService>(client =>
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
    
    SemanticChunker chunker = new(embeddingGenerator, tokenLimit: options.TokenLimit);
    
    return chunker;
});
builder.Services.AddSingleton<ITextChunkingStrategy, SemanticChunkerStrategy>();

// Register services
builder.Services.AddSingleton<IOllamaEmbeddingService, OllamaEmbeddingService>();
builder.Services.AddSingleton<IDocumentChunkingService, DocumentChunkingService>();

// Configure message publishing and consuming using RabbitMQ.Client (LavinMQ compatible)
IConnectionFactory connectionFactory = RabbitMqConnectionFactory.CreateConnectionFactory(builder.Configuration);
builder.Services.AddSingleton(connectionFactory);
builder.Services.AddSingleton<IMessagePublisher, MessagePublisher>();

// Register consumer
builder.Services.AddSingleton<IMessageConsumer<DocumentCrackedMessage>, DocumentCrackedConsumer>();

// Register consumer service (background service)
builder.Services.AddHostedService<MessageConsumerService<DocumentCrackedMessage>>();

// Configure OpenTelemetry ActivitySource
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

// Register ActivitySource for dependency injection
builder.Services.AddSingleton(activitySource);

// Build host
using IHost host = builder.Build();

ILogger<Program> logger = host.Services.GetRequiredService<ILogger<Program>>();

logger.LogInformation("Starting Document Chunking Worker");
logger.LogInformation("Ollama Endpoint: {Endpoint}", ollamaEndpoint);
logger.LogInformation("Ollama Model: {Model}", ollamaModel);
logger.LogInformation("Worker ready and listening for DocumentCrackedMessage on queue");

await host.RunAsync();

