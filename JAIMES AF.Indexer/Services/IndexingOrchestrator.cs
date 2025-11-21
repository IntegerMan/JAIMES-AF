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
                    logger.LogWarning("Indexing process cancelled");
                    break;
                }

                // TODO: Remove this
                if (!directory.ToLowerInvariant().Contains("dnd")) {
                    logger.LogInformation("Skipping directory: {Directory} because it does not contain 'dnd'", directory);
                    continue;
                }

                DirectoryInfo directoryInfo = new DirectoryInfo(directory);
                string indexName = directoryInfo.Name.ToLowerInvariant();
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
        string[] files = directoryScanner.GetFiles(directoryPath, options.SupportedExtensions).ToArray();

        foreach (string filePath in files)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            summary.TotalProcessed++;
            try
            {
                string id = await documentIndexer.IndexDocumentAsync(filePath, indexName, cancellationToken);
                logger.LogInformation("Successfully indexed file: {FilePath} as {Id}", filePath, id);
                summary.TotalSucceeded++;
            }
            catch (Exception ex)
            {
                summary.TotalErrors++;
                logger.LogError(ex, "Unexpected error processing file: {FilePath}", filePath);
            }
        }

        return summary;
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

