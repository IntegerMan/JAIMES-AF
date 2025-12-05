using System.Diagnostics;
using MattEland.Jaimes.DocumentCracker.Services;
using MattEland.Jaimes.DocumentProcessing.Options;
using MattEland.Jaimes.DocumentProcessing.Services;
using MattEland.Jaimes.ServiceDefaults;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.Repositories;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RabbitMQ.Client;
using Spectre.Console;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Configure OpenTelemetry for Aspire telemetry
// This sets up OTLP exporter, logging, metrics, and tracing
builder.ConfigureOpenTelemetry();

// Verify OTLP exporter is configured (for debugging)
string? otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] 
    ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
if (string.IsNullOrWhiteSpace(otlpEndpoint))
{
    // Log warning but don't fail - exporter might be configured differently
    Console.WriteLine("WARNING: OTEL_EXPORTER_OTLP_ENDPOINT not found - telemetry may not be exported to Aspire");
}
else
{
    Console.WriteLine($"OTLP Exporter configured: {otlpEndpoint}");
}

// Configure logging with OpenTelemetry
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
});

builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
    .AddUserSecrets(typeof(Program).Assembly, optional: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

DocumentScanOptions options = BindOptions(builder);

// Add EF Core database context
builder.Services.AddDbContext<MattEland.Jaimes.Repositories.JaimesDbContext>(options =>
{
    string? connectionString = builder.Configuration.GetConnectionString("postgres-db");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("Connection string 'postgres-db' is required.");
    }
    options.UseNpgsql(connectionString);
});

// Configure message publishing using RabbitMQ.Client (LavinMQ compatible)
IConnectionFactory connectionFactory = RabbitMqConnectionFactory.CreateConnectionFactory(builder.Configuration);
builder.Services.AddSingleton(connectionFactory);
builder.Services.AddSingleton<IMessagePublisher, MessagePublisher>();

// Configure OpenTelemetry ActivitySource for document cracking
const string activitySourceName = "Jaimes.DocumentCracker";
ActivitySource activitySource = new(activitySourceName);

// Register ActivitySource with OpenTelemetry tracing
// The issue: Multiple AddOpenTelemetry() calls should be additive, but the ActivitySource isn't being registered.
// The wildcard "Jaimes.*" in ConfigureOpenTelemetry should match "Jaimes.DocumentCracker", but it's not working.
// We're explicitly registering it here to ensure it's registered.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: activitySourceName, serviceVersion: "1.0.0"))
    .WithMetrics(metrics =>
    {
        metrics.AddRuntimeInstrumentation()
            .AddHttpClientInstrumentation()
            .AddMeter(activitySourceName);
    })
    .WithTracing(tracing =>
    {
        // CRITICAL: Explicitly add the ActivitySource - this MUST register the listener
        // If this doesn't work, the issue is that multiple AddOpenTelemetry() calls aren't chaining properly
        tracing.AddSource(activitySourceName);
    });

// Register ActivitySource for dependency injection
builder.Services.AddSingleton(activitySource);

builder.Services.AddSingleton(options);
builder.Services.AddSingleton<IDirectoryScanner, DirectoryScanner>();

// Register DbContext factory wrapper for console app (uses singleton DbContext)
builder.Services.AddSingleton<IDbContextFactory<JaimesDbContext>>(sp =>
{
    JaimesDbContext dbContext = sp.GetRequiredService<JaimesDbContext>();
    return new SingletonDbContextFactory(dbContext);
});

// Register shared worker services
builder.Services.AddSingleton<IPdfTextExtractor, MattEland.Jaimes.Workers.Services.PdfPigTextExtractor>();
builder.Services.AddSingleton<IDocumentCrackingService, MattEland.Jaimes.Workers.Services.DocumentCrackingService>();
builder.Services.AddSingleton<DocumentCrackingOrchestrator>();

using IHost host = builder.Build();

    // CRITICAL: Start the host to ensure OpenTelemetry TracerProvider is built and ActivitySource listeners are registered
    // The TracerProvider is only active after the host is started
    await host.StartAsync();

ILogger<Program> logger = host.Services.GetRequiredService<ILogger<Program>>();
DocumentCrackingOrchestrator orchestrator = host.Services.GetRequiredService<DocumentCrackingOrchestrator>();

try
{
    AnsiConsole.Write(new FigletText("JAIMES Cracker").Color(Color.Orange1));
    AnsiConsole.MarkupLine("[bold orange1]Text extraction utility[/]");
    AnsiConsole.WriteLine();

    Panel infoPanel = new Panel(
        new Table()
            .AddColumn(new TableColumn("[bold]Setting[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Value[/]"))
            .HideHeaders()
            .Border(TableBorder.None)
            .AddRow("[dim]Source Directory:[/]", $"[cyan]{options.SourceDirectory}[/]")
            .AddRow("[dim]Storage:[/]", "[cyan]PostgreSQL[/]")
            .AddRow("[dim]Supported Extensions:[/]", $"[cyan]{string.Join(", ", options.SupportedExtensions)}[/]"))
    {
        Header = new PanelHeader("[bold yellow]Configuration[/]", Justify.Left),
        Border = BoxBorder.Rounded,
        BorderStyle = new Style(Color.Yellow1)
    };

    AnsiConsole.Write(infoPanel);
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold green]Starting cracking process...[/]");

    // Wrap the entire cracking process in a trace
    // IMPORTANT: Get ActivitySource AFTER host is started so TracerProvider is built and listeners are registered
    ActivitySource mainActivitySource = host.Services.GetRequiredService<ActivitySource>();
    
    // Verify ActivitySource has listeners (for debugging)
    if (!mainActivitySource.HasListeners())
    {
        logger.LogWarning("ActivitySource '{ActivitySourceName}' has no listeners - activities will not be created. Check OpenTelemetry configuration.", activitySourceName);
    }
    else
    {
        logger.LogDebug("ActivitySource '{ActivitySourceName}' has listeners registered", activitySourceName);
    }
    
    using Activity? mainActivity = mainActivitySource.StartActivity("DocumentCracking.ProcessAll");
    
    if (mainActivity == null)
    {
        logger.LogWarning("Failed to create main activity - ActivitySource may not be registered or sampled. ActivitySource name: {ActivitySourceName}", activitySourceName);
    }
    else
    {
        logger.LogDebug("Created main activity: {ActivityId}", mainActivity.Id);
    }
    
    mainActivity?.SetTag("cracker.source_directory", options.SourceDirectory);
    mainActivity?.SetTag("cracker.supported_extensions", string.Join(", ", options.SupportedExtensions));

    DocumentCrackingOrchestrator.DocumentCrackingSummary summary =
        await orchestrator.CrackAllAsync(CancellationToken.None);
    
    if (mainActivity != null)
    {
        mainActivity.SetTag("cracker.total_discovered", summary.TotalDiscovered);
        mainActivity.SetTag("cracker.total_cracked", summary.TotalCracked);
        mainActivity.SetTag("cracker.total_failures", summary.TotalFailures);
        mainActivity.SetTag("cracker.skipped_unsupported", summary.SkippedUnsupported);
        mainActivity.SetStatus(summary.TotalFailures > 0 ? ActivityStatusCode.Error : ActivityStatusCode.Ok);
    }

    Table summaryTable = new Table()
        .AddColumn(new TableColumn("[bold]Metric[/]").RightAligned())
        .AddColumn(new TableColumn("[bold]Value[/]").Centered())
        .HideHeaders()
        .Border(TableBorder.Rounded)
        .AddRow("[dim]Files Discovered:[/]", $"[cyan]{summary.TotalDiscovered}[/]")
        .AddRow("[dim]Cracked:[/]", $"[green]{summary.TotalCracked}[/]")
        .AddRow("[dim]Unsupported:[/]", $"[yellow]{summary.SkippedUnsupported}[/]")
        .AddRow("[dim]Failures:[/]", summary.TotalFailures > 0 ? $"[red]{summary.TotalFailures}[/]" : "[green]0[/]");

    Panel summaryPanel = new Panel(summaryTable)
    {
        Header = new PanelHeader("[bold green]✓ Cracking Complete[/]", Justify.Left),
        Border = BoxBorder.Rounded,
        BorderStyle = new Style(Color.Green1)
    };

    AnsiConsole.WriteLine();
    AnsiConsole.Write(summaryPanel);
    AnsiConsole.WriteLine();

    return summary.TotalFailures > 0 ? 1 : 0;
}
catch (Exception ex)
{
    logger.LogError(ex, "Fatal error during document cracking.");
    AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
    return 1;
}
finally
{
    // Stop the host to ensure telemetry is flushed
    await host.StopAsync();
}

static DocumentScanOptions BindOptions(HostApplicationBuilder builder)
{
    DocumentScanOptions options = new();
    builder.Configuration.GetSection("DocumentCracker").Bind(options);

    if (options.SupportedExtensions.Count == 0)
    {
        options.SupportedExtensions = [".pdf"];
    }

    options.SupportedExtensions = options.SupportedExtensions
        .Where(ext => !string.IsNullOrWhiteSpace(ext))
        .Select(ext => ext.Trim())
        .Where(ext => ext.StartsWith(".", StringComparison.Ordinal))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    if (string.IsNullOrWhiteSpace(options.SourceDirectory))
    {
        throw new InvalidOperationException("DocumentCracker SourceDirectory is not configured.");
    }

    options.SourceDirectory = Path.GetFullPath(options.SourceDirectory);

    return options;
}
