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
// Enable debug logging for Kernel Memory and OpenAI to see API calls
builder.Logging.AddFilter("Microsoft.KernelMemory", LogLevel.Debug);
builder.Logging.AddFilter("Microsoft.SemanticKernel", LogLevel.Debug);
builder.Logging.AddFilter("OpenAI", LogLevel.Debug);

// Load configuration
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
    .AddUserSecrets(typeof(Program).Assembly)
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

// Configure Kernel Memory
// Normalize endpoint URL - remove trailing slash to avoid 404 errors
string normalizedEndpoint = options.OpenAiEndpoint.TrimEnd('/');

AzureOpenAIConfig openAiConfig = new()
{
    APIKey = options.OpenAiApiKey,
    Auth = AzureOpenAIConfig.AuthTypes.APIKey,
    Endpoint = normalizedEndpoint,
    Deployment = options.OpenAiDeployment,
};

// Extract file path from connection string if needed
// WithSimpleVectorDb expects just the file path, not a full connection string
string vectorDbPath = options.VectorDbConnectionString;
if (vectorDbPath.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
{
    vectorDbPath = vectorDbPath["Data Source=".Length..].Trim();
}

IKernelMemory memory = new KernelMemoryBuilder()
    .WithAzureOpenAITextEmbeddingGeneration(openAiConfig)
    .WithoutTextGenerator()
    .WithSimpleVectorDb(vectorDbPath)
    .Build();

builder.Services.AddSingleton(memory);

// Register application services
builder.Services.AddSingleton<IDirectoryScanner, DirectoryScanner>();
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
    logger.LogInformation("Vector DB path: {VectorDbPath}", vectorDbPath);
    logger.LogInformation("Supported extensions: {Extensions}", string.Join(", ", options.SupportedExtensions));
    logger.LogInformation("=== OpenAI Configuration ===");
    logger.LogInformation("OpenAI Endpoint: {Endpoint}", normalizedEndpoint);
    logger.LogInformation("OpenAI Embedding Deployment Name: '{Deployment}'", options.OpenAiDeployment);
    logger.LogInformation("OpenAI API Key Length: {KeyLength} characters", string.IsNullOrEmpty(options.OpenAiApiKey) ? 0 : options.OpenAiApiKey.Length);
    
    // Log the expected URI format that Azure OpenAI will construct
    string expectedEmbeddingsUri = $"{normalizedEndpoint}/openai/deployments/{options.OpenAiDeployment}/embeddings?api-version=2024-02-15-preview";
    logger.LogInformation("Expected Embeddings API URI: {Uri}", expectedEmbeddingsUri);
    logger.LogInformation("=== End OpenAI Configuration ===");
    
    // Warn if deployment name doesn't match common patterns (might indicate incorrect name)
    if (!options.OpenAiDeployment.Contains("embedding", StringComparison.OrdinalIgnoreCase) && 
        !options.OpenAiDeployment.Contains("ada", StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning(
            "Deployment name '{Deployment}' doesn't match common embedding model patterns. " +
            "If you encounter 404 errors, verify this matches your Azure OpenAI deployment name exactly (case-sensitive). " +
            "Common names: 'text-embedding-3-small-global', 'text-embedding-3-small', 'text-embedding-ada-002'",
            options.OpenAiDeployment);
    }

    IndexingOrchestrator.IndexingSummary summary = await orchestrator.ProcessAllDirectoriesAsync(CancellationToken.None);

    logger.LogInformation("Indexing completed successfully. Processed: {Processed}, Succeeded: {Succeeded}, Errors: {Errors}",
        summary.TotalProcessed, summary.TotalSucceeded, summary.TotalErrors);
    return 0;
}
catch (Exception ex)
{
    logger.LogError(ex, "Fatal error during indexing");
    return 1;
}
