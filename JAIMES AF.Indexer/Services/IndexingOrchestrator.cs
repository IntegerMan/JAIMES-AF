using Microsoft.Extensions.Logging;
using Spectre.Console;
using MattEland.Jaimes.Indexer.Configuration;

namespace MattEland.Jaimes.Indexer.Services;

public class IndexingOrchestrator(
    ILogger<IndexingOrchestrator> logger,
    IDirectoryScanner directoryScanner,
    IDocumentIndexer documentIndexer,
    IndexerOptions options)
{
    public async Task<IndexingSummary> ProcessAllDirectoriesAsync(CancellationToken cancellationToken = default)
    {
        IndexingSummary summary = new();

        try
        {
            IEnumerable<string> subdirectories = directoryScanner.GetSubdirectories(options.SourceDirectory);
            List<string> allDirectories = subdirectories.ToList();
            
            // Add root directory to the list
            allDirectories.Add(options.SourceDirectory);
            
            int totalDirectories = allDirectories.Count;
            int currentDirectory = 0;

            await AnsiConsole.Progress()
                .AutoRefresh(true)
                .AutoClear(false)
                .HideCompleted(false)
                .Columns(new ProgressColumn[]
                {
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new SpinnerColumn()
                })
                .StartAsync(async ctx =>
                {
                    ProgressTask mainTask = ctx.AddTask("[bold cyan]Processing directories[/]", maxValue: totalDirectories);

                    foreach (string directory in allDirectories)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            AnsiConsole.MarkupLine("[yellow]⚠[/] [yellow]Indexing process cancelled[/]");
                            logger.LogWarning("Indexing process cancelled");
                            break;
                        }

                        currentDirectory++;
                        string indexName = GetIndexName(directory);
                        string displayName = directory == options.SourceDirectory ? "[bold]Root Directory[/]" : $"[bold]{Path.GetFileName(directory)}[/]";
                        
                        mainTask.Description = $"[cyan]Processing: {displayName}[/]";

                        IndexingSummary dirSummary = await ProcessDirectoryAsync(directory, indexName, cancellationToken, ctx);
                        summary.Add(dirSummary);
                        
                        mainTask.Increment(1);
                    }
                });
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] [red]Error during indexing process: {ex.Message}[/]");
            // Only log full exception details to logger for debugging, not to console
            logger.LogError(ex, "Error during indexing process");
            throw;
        }

        return summary;
    }

    private async Task<IndexingSummary> ProcessDirectoryAsync(string directoryPath, string indexName, CancellationToken cancellationToken, ProgressContext? progressContext = null)
    {
        IndexingSummary summary = new();
        List<string> files = directoryScanner.GetFiles(directoryPath, options.SupportedExtensions).ToList();
        
        if (files.Count == 0)
        {
            return summary;
        }

        string directoryName = Path.GetFileName(directoryPath);
        if (string.IsNullOrEmpty(directoryName))
        {
            directoryName = "Root";
        }

        ProgressTask? fileTask = progressContext?.AddTask(
            $"[dim]  Files in {directoryName}[/]",
            maxValue: files.Count);

        foreach (string filePath in files)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            string fileName = Path.GetFileName(filePath);
            fileTask?.Description($"[dim]  Processing: [cyan]{fileName}[/][/]");

            summary.TotalProcessed++;
            try
            {
                bool result = await ProcessFileAsync(filePath, indexName, cancellationToken);

                if (result)
                {
                    summary.TotalSucceeded++;
                    fileTask?.Description($"[dim]  [green]✓[/] [green]{fileName}[/][/]");
                }
                else
                {
                    summary.TotalErrors++;
                    fileTask?.Description($"[dim]  [red]✗[/] [red]{fileName}[/][/]");
                }
            }
            catch (Exception ex)
            {
                summary.TotalErrors++;
                fileTask?.Description($"[dim]  [red]✗[/] [red]{fileName}[/] ([red]{Markup.Escape(ex.Message)}[/])[/]");
                // Only log full exception details to logger for debugging
                logger.LogError(ex, "Unexpected error processing file: {FilePath}", filePath);
            }
            
            fileTask?.Increment(1);
        }

        return summary;
    }

    private async Task<bool> ProcessFileAsync(string filePath, string indexName, CancellationToken cancellationToken)
    {
        try
        {
            return await documentIndexer.IndexDocumentAsync(filePath, indexName, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing file: {FilePath}", filePath);
            return false;
        }
    }

    private static string GetIndexName(string directoryPath)
    {
        // Use directory name as index name, normalized
        string directoryName = Path.GetFileName(directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrEmpty(directoryName))
        {
            directoryName = "root";
        }
        return $"index-{directoryName.ToLowerInvariant().Replace(" ", "-")}";
    }

    public class IndexingSummary
    {
        public int TotalProcessed { get; set; }
        public int TotalSucceeded { get; set; }
        public int TotalErrors { get; set; }

        public void Add(IndexingSummary other)
        {
            TotalProcessed += other.TotalProcessed;
            TotalSucceeded += other.TotalSucceeded;
            TotalErrors += other.TotalErrors;
        }
    }
}

