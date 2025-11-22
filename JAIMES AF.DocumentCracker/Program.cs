using MattEland.Jaimes.DocumentCracker.Services;
using MattEland.Jaimes.DocumentProcessing.Options;
using MattEland.Jaimes.DocumentProcessing.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Add Seq endpoint for advanced log monitoring
builder.AddSeqEndpoint("seq");

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
    .AddUserSecrets(typeof(Program).Assembly, optional: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

DocumentScanOptions options = BindOptions(builder);

// Add MongoDB client integration
builder.AddMongoDBClient("documents");

builder.Services.AddSingleton(options);
builder.Services.AddSingleton<IDirectoryScanner, DirectoryScanner>();
builder.Services.AddSingleton<DocumentCrackingOrchestrator>();

using IHost host = builder.Build();

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
            .AddRow("[dim]Storage:[/]", "[cyan]MongoDB (documents database)[/]")
            .AddRow("[dim]Supported Extensions:[/]", $"[cyan]{string.Join(", ", options.SupportedExtensions)}[/]"))
    {
        Header = new PanelHeader("[bold yellow]Configuration[/]", Justify.Left),
        Border = BoxBorder.Rounded,
        BorderStyle = new Style(Color.Yellow1)
    };

    AnsiConsole.Write(infoPanel);
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold green]Starting cracking process...[/]");

    DocumentCrackingOrchestrator.DocumentCrackingSummary summary =
        await orchestrator.CrackAllAsync(CancellationToken.None);

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

static DocumentScanOptions BindOptions(HostApplicationBuilder builder)
{
    DocumentScanOptions options = new();
    builder.Configuration.GetSection("DocumentCracker").Bind(options);

    bool requiresFallback = string.IsNullOrWhiteSpace(options.SourceDirectory) ||
        options.SupportedExtensions.Count == 0;

    if (requiresFallback)
    {
        DocumentScanOptions? indexerOptions = builder.Configuration
            .GetSection("Indexer")
            .Get<DocumentScanOptions>();

        if (indexerOptions != null)
        {
            if (string.IsNullOrWhiteSpace(options.SourceDirectory))
            {
                options.SourceDirectory = indexerOptions.SourceDirectory;
            }

            if (options.SupportedExtensions.Count == 0 &&
                indexerOptions.SupportedExtensions is { Count: > 0 })
            {
                options.SupportedExtensions = indexerOptions.SupportedExtensions;
            }
        }
    }

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
