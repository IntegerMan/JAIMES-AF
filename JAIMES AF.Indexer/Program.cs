using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.MemoryDb.Redis;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Spectre.Console;
using MattEland.Jaimes.DocumentProcessing.Services;
using MattEland.Jaimes.Indexer.Configuration;
using MattEland.Jaimes.Indexer.Services;
using MattEland.Jaimes.Services.Configuration;
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
IndexerOptions options = builder.Configuration.GetSection("Indexer").Get<IndexerOptions>()
    ?? throw new InvalidOperationException("Indexer configuration section is required");

// Validate required configuration
if (string.IsNullOrWhiteSpace(options.SourceDirectory))
{
    throw new InvalidOperationException("SourceDirectory configuration is required");
}

if (string.IsNullOrWhiteSpace(options.OllamaEndpoint) || string.IsNullOrWhiteSpace(options.OllamaModel))
{
    throw new InvalidOperationException("Ollama configuration (Endpoint and Model) is required");
}

// Register services
builder.Services.AddSingleton(options);

// Configure Kernel Memory
// Get the Ollama endpoint - Aspire should resolve connection string expressions at runtime
string? rawEndpoint = options.OllamaEndpoint;

if (string.IsNullOrWhiteSpace(rawEndpoint))
{
    throw new InvalidOperationException(
        "Ollama endpoint is not configured. " +
        "Expected a valid absolute URI (e.g., 'http://localhost:11434'). " +
        "Check that the Ollama endpoint is properly configured in AppHost.");
}

// Normalize endpoint URL - remove trailing slash to avoid 404 errors
string ollamaEndpoint = rawEndpoint.TrimEnd('/');

// If the endpoint contains connection string expressions (curly braces), it hasn't been resolved by Aspire
if (ollamaEndpoint.Contains('{'))
{
    // Try to get the endpoint from the Ollama connection string that Aspire injects
    string? ollamaConnectionString = builder.Configuration.GetConnectionString("ollama-models")
        ?? builder.Configuration["ConnectionStrings:ollama-models"]
        ?? builder.Configuration["ConnectionStrings__ollama-models"];
    
    if (!string.IsNullOrWhiteSpace(ollamaConnectionString) && Uri.TryCreate(ollamaConnectionString, UriKind.Absolute, out Uri? connectionUri))
    {
        ollamaEndpoint = connectionUri.ToString().TrimEnd('/');
    }
    else
    {
        throw new InvalidOperationException(
            $"Ollama endpoint contains unresolved connection string expressions: '{ollamaEndpoint}'. " +
            "This indicates Aspire did not resolve the endpoint expression. " +
            "Check that the Ollama endpoint is properly configured in AppHost.cs.");
    }
}

// Validate that the Ollama endpoint is a valid URI
if (!Uri.TryCreate(ollamaEndpoint, UriKind.Absolute, out Uri? validatedUri))
{
    throw new InvalidOperationException(
        $"Invalid Ollama endpoint URI: '{ollamaEndpoint}'. " +
        "Expected a valid absolute URI (e.g., 'http://localhost:11434'). " +
        "Check that the Ollama endpoint is properly configured in AppHost.");
}

// Use Redis as the vector store for Kernel Memory
// Redis provides better performance and document listing capabilities than SimpleVectorDb
// Connection string format: "localhost:6379" or "localhost:6379,password=xxx" or full connection string
string redisConnectionString = options.VectorDbConnectionString;

// Use centralized RedisConfig creation to ensure consistency
RedisConfig redisConfig = RedisConfigHelper.CreateRedisConfig(redisConnectionString);

// Use Redis as the vector store for Kernel Memory
// Note: WithOllamaTextEmbeddingGeneration parameter order is (model, endpoint)
IKernelMemory memory = new KernelMemoryBuilder()
    .WithOllamaTextEmbeddingGeneration(options.OllamaModel, ollamaEndpoint)
    .WithoutTextGenerator()
    .WithRedisMemoryDb(redisConfig)
    .Build();

builder.Services.AddSingleton(memory);

// Configure OpenTelemetry ActivitySource
const string activitySourceName = "Jaimes.Indexer";
ActivitySource activitySource = new(activitySourceName);

// Add ActivitySource to OpenTelemetry tracing (ConfigureOpenTelemetry already sets up OTLP exporter)
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
            .AddHttpClientInstrumentation()
            .AddRedisInstrumentation();
    });

// Register ActivitySource for dependency injection
builder.Services.AddSingleton(activitySource);

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
            .AddRow("[dim]Ollama Endpoint:[/]", $"[cyan]{ollamaEndpoint}[/]")
            .AddRow("[dim]Embedding Model:[/]", $"[cyan]{options.OllamaModel}[/]")
    )
    {
        Header = new PanelHeader("[bold yellow]Configuration[/]", Justify.Left),
        Border = BoxBorder.Rounded,
        BorderStyle = new Style(Color.Yellow1)
    };

    AnsiConsole.Write(configPanel);
    AnsiConsole.WriteLine();

    AnsiConsole.MarkupLine("[bold green]Starting indexing process...[/]");
    AnsiConsole.WriteLine();

    // Wrap the entire indexing process in a trace
    ActivitySource mainActivitySource = host.Services.GetRequiredService<ActivitySource>();
    using Activity? mainActivity = mainActivitySource.StartActivity("Indexing.ProcessAll");
    mainActivity?.SetTag("indexer.source_directory", options.SourceDirectory);
    mainActivity?.SetTag("indexer.ollama_endpoint", ollamaEndpoint);
    mainActivity?.SetTag("indexer.ollama_model", options.OllamaModel);

    IndexingOrchestrator.IndexingSummary summary = await orchestrator.ProcessAllDirectoriesAsync(CancellationToken.None);
    
    if (mainActivity != null)
    {
        mainActivity.SetTag("indexer.total_processed", summary.TotalProcessed);
        mainActivity.SetTag("indexer.total_succeeded", summary.TotalSucceeded);
        mainActivity.SetTag("indexer.total_errors", summary.TotalErrors);
        mainActivity.SetStatus(summary.TotalErrors > 0 ? ActivityStatusCode.Error : ActivityStatusCode.Ok);
    }

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
    AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
    logger.LogError(ex, "Fatal error during indexing");
    return 1;
}
