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

            foreach (string directory in allDirectories)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    AnsiConsole.MarkupLine("[yellow]⚠[/] [yellow]Indexing process cancelled[/]");
                    logger.LogWarning("Indexing process cancelled");
                    break;
                }

                string indexName = GetIndexName(directory);
                logger.LogInformation("Processing directory: {Directory} -> Index name: {IndexName}", directory, indexName);

                IndexingSummary dirSummary = await ProcessDirectoryAsync(directory, indexName, cancellationToken);
                summary.Add(dirSummary);
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] [red]Error during indexing process: {ex.Message}[/]");
            logger.LogError(ex, "Error during indexing process");
            throw;
        }

        return summary;
    }

    private async Task<IndexingSummary> ProcessDirectoryAsync(string directoryPath, string indexName, CancellationToken cancellationToken)
    {
        IndexingSummary summary = new();
        List<string> files = directoryScanner.GetFiles(directoryPath, options.SupportedExtensions).ToList();
        
        if (files.Count == 0)
        {
            return summary;
        }

        foreach (string filePath in files)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            summary.TotalProcessed++;
            
            try
            {
                bool result = await ProcessFileAsync(filePath, indexName, cancellationToken);

                if (result)
                {
                    summary.TotalSucceeded++;
                }
                else
                {
                    summary.TotalErrors++;
                }
            }
            catch (Exception ex)
            {
                summary.TotalErrors++;
                logger.LogError(ex, "Unexpected error processing file: {FilePath}", filePath);
            }
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

