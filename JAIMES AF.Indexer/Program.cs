using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using MattEland.Jaimes.Indexer.Configuration;
using MattEland.Jaimes.Indexer.Services;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Load configuration
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

// Bind configuration
IndexerOptions options = builder.Configuration.GetSection("Indexer").Get<IndexerOptions>()
    ?? throw new InvalidOperationException("Indexer configuration section is required");

// Validate required configuration
if (string.IsNullOrWhiteSpace(options.SourceDirectory))
{
    throw new InvalidOperationException("SourceDirectory configuration is required");
}

if (string.IsNullOrWhiteSpace(options.OpenAiEndpoint) || string.IsNullOrWhiteSpace(options.OpenAiApiKey))
{
    throw new InvalidOperationException("OpenAI configuration (Endpoint and ApiKey) is required");
}

// Register services
builder.Services.AddSingleton(options);

// Configure Kernel Memory - only embedding model needed for indexing
OpenAIConfig openAiConfig = new()
{
    APIKey = options.OpenAiApiKey,
    Endpoint = options.OpenAiEndpoint,
    EmbeddingModel = options.OpenAiDeployment
};

IKernelMemory memory = new KernelMemoryBuilder()
    .WithOpenAI(openAiConfig)
    .WithSimpleVectorDb(options.VectorDbConnectionString)
    .Build();

builder.Services.AddSingleton(memory);

// Register application services
builder.Services.AddSingleton<IDirectoryScanner, DirectoryScanner>();
builder.Services.AddSingleton<IChangeTracker, ChangeTracker>();
builder.Services.AddSingleton<IDocumentIndexer, DocumentIndexer>();
builder.Services.AddSingleton<IndexingOrchestrator>();

// Build host
using IHost host = builder.Build();

// Get services
ILogger<Program> logger = host.Services.GetRequiredService<ILogger<Program>>();
IndexingOrchestrator orchestrator = host.Services.GetRequiredService<IndexingOrchestrator>();

try
{
    logger.LogInformation("Starting document indexing application");
    logger.LogInformation("Source directory: {SourceDirectory}", options.SourceDirectory);
    logger.LogInformation("Vector DB connection: {VectorDbConnectionString}", options.VectorDbConnectionString);
    logger.LogInformation("Supported extensions: {Extensions}", string.Join(", ", options.SupportedExtensions));

    IndexingOrchestrator.IndexingSummary summary = await orchestrator.ProcessAllDirectoriesAsync(CancellationToken.None);

    logger.LogInformation("Indexing completed successfully. Processed: {Processed}, Added: {Added}, Updated: {Updated}, Errors: {Errors}",
        summary.TotalProcessed, summary.TotalAdded, summary.TotalUpdated, summary.TotalErrors);
    return 0;
}
catch (Exception ex)
{
    logger.LogError(ex, "Fatal error during indexing");
    return 1;
}
