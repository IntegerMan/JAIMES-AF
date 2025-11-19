using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.MemoryDb.Redis;
using Spectre.Console;
using MattEland.Jaimes.Indexer.Configuration;
using MattEland.Jaimes.Indexer.Services;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Configure logging - keep minimal to avoid cluttering Spectre.Console output
// Only log warnings and errors to console, suppress informational messages
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Warning);
// Suppress verbose logging from Kernel Memory and OpenAI during indexing
builder.Logging.AddFilter("Microsoft.KernelMemory", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.SemanticKernel", LogLevel.Warning);
builder.Logging.AddFilter("OpenAI", LogLevel.Warning);
// Suppress our own informational logging - we use Spectre.Console instead
builder.Logging.AddFilter("MattEland.Jaimes.Indexer", LogLevel.Warning);

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

// Use Redis as the vector store for Kernel Memory
// Redis provides better performance and document listing capabilities than SimpleVectorDb
// Connection string format: "localhost:6379" or "localhost:6379,password=xxx" or full connection string
string redisConnectionString = options.VectorDbConnectionString;

// If connection string is in old format (Data Source=...), extract just the path
// Otherwise use as-is for Redis connection string
if (redisConnectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
{
    // Legacy format - convert to Redis default
    redisConnectionString = "localhost:6379";
}

// Configure Redis with tag fields that Kernel Memory uses internally and that we use in our code
// IMPORTANT: All tag fields used when indexing documents MUST be declared here, or Redis will throw
// an "un-indexed tag field" error. This includes:
// - System tags: __part_n (document parts), collection (document organization)
// - Document tags: sourcePath, fileName (used by DocumentIndexer)
// - Rule tags: rulesetId, ruleId, title (used by RulesSearchService)
// See: https://github.com/microsoft/kernel-memory/discussions/735
RedisConfig redisConfig = new("km-", new Dictionary<string, char?>
{
    // System tags used by Kernel Memory internally
    { "__part_n", ',' },
    { "collection", ',' },
    // Document tags used by DocumentIndexer
    { "sourcePath", ',' },
    { "fileName", ',' },
    // Rule tags used by RulesSearchService
    { "rulesetId", ',' },
    { "ruleId", ',' },
    { "title", ',' }
});
redisConfig.ConnectionString = redisConnectionString;

// Use Redis as the vector store for Kernel Memory
IKernelMemory memory = new KernelMemoryBuilder()
    .WithAzureOpenAITextEmbeddingGeneration(openAiConfig)
    .WithoutTextGenerator()
    .WithRedisMemoryDb(redisConfig)
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
    // Display colorful header
    AnsiConsole.Write(new FigletText("JAIMES Indexer").Color(Color.Cyan1));
    AnsiConsole.MarkupLine("[bold cyan]Document Indexing Application[/]");
    AnsiConsole.WriteLine();

    // Display configuration panel
    Panel configPanel = new Panel(
        new Table()
            .AddColumn(new TableColumn("[bold]Setting[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Value[/]"))
            .HideHeaders()
            .Border(TableBorder.None)
            .AddRow("[dim]Source Directory:[/]", $"[cyan]{options.SourceDirectory}[/]")
            .AddRow("[dim]Redis Connection:[/]", $"[cyan]{redisConnectionString}[/]")
            .AddRow("[dim]Supported Extensions:[/]", $"[cyan]{string.Join(", ", options.SupportedExtensions)}[/]")
            .AddRow("[dim]OpenAI Endpoint:[/]", $"[cyan]{normalizedEndpoint}[/]")
            .AddRow("[dim]Embedding Deployment:[/]", $"[cyan]{options.OpenAiDeployment}[/]")
            .AddRow("[dim]API Key Length:[/]", $"[cyan]{options.OpenAiApiKey.Length} characters[/]")
    )
    {
        Header = new PanelHeader("[bold yellow]Configuration[/]", Justify.Left),
        Border = BoxBorder.Rounded,
        BorderStyle = new Style(Color.Yellow1)
    };

    AnsiConsole.Write(configPanel);
    AnsiConsole.WriteLine();

    // Warn if deployment name doesn't match common patterns
    if (!options.OpenAiDeployment.Contains("embedding", StringComparison.OrdinalIgnoreCase) && 
        !options.OpenAiDeployment.Contains("ada", StringComparison.OrdinalIgnoreCase))
    {
        AnsiConsole.MarkupLine("[yellow]⚠[/] [yellow]Warning:[/] Deployment name doesn't match common embedding model patterns.");
        AnsiConsole.MarkupLine("[dim]If you encounter 404 errors, verify this matches your Azure OpenAI deployment name exactly.[/]");
        AnsiConsole.WriteLine();
    }

    AnsiConsole.MarkupLine("[bold green]Starting indexing process...[/]");
    AnsiConsole.WriteLine();

    IndexingOrchestrator.IndexingSummary summary = await orchestrator.ProcessAllDirectoriesAsync(CancellationToken.None);

    AnsiConsole.WriteLine();
    
    // Display summary table
    Table summaryTable = new Table()
        .AddColumn(new TableColumn("[bold]Metric[/]").RightAligned())
        .AddColumn(new TableColumn("[bold]Value[/]").Centered())
        .HideHeaders()
        .Border(TableBorder.Rounded)
        .AddRow("[dim]Total Processed:[/]", $"[cyan]{summary.TotalProcessed}[/]")
        .AddRow("[dim]Succeeded:[/]", $"[green]{summary.TotalSucceeded}[/]")
        .AddRow("[dim]Errors:[/]", summary.TotalErrors > 0 ? $"[red]{summary.TotalErrors}[/]" : "[green]0[/]");

    Panel summaryPanel = new Panel(summaryTable)
    {
        Header = new PanelHeader("[bold green]✓ Indexing Complete[/]", Justify.Left),
        Border = BoxBorder.Rounded,
        BorderStyle = new Style(Color.Green1)
    };

    AnsiConsole.Write(summaryPanel);
    AnsiConsole.WriteLine();
    
    return summary.TotalErrors > 0 ? 1 : 0;
}
catch (Exception ex)
{
    AnsiConsole.WriteLine();
    AnsiConsole.WriteException(ex, ExceptionFormats.ShortenPaths | ExceptionFormats.ShortenTypes);
    logger.LogError(ex, "Fatal error during indexing");
    return 1;
}
