using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using MattEland.Jaimes.DocumentProcessing.Services;
using MattEland.Jaimes.Indexer.Configuration;

namespace MattEland.Jaimes.Indexer.Services;

public class IndexingOrchestrator(
    ILogger<IndexingOrchestrator> logger,
    IDirectoryScanner directoryScanner,
    IDocumentIndexer documentIndexer,
    IndexerOptions options,
    ActivitySource activitySource)
{
    public async Task<IndexingSummary> ProcessAllDirectoriesAsync(CancellationToken cancellationToken = default)
    {
        IndexingSummary summary = new();

        try
        {
            string sourceDirectory = options.SourceDirectory
                ?? throw new InvalidOperationException("Indexer source directory is not configured.");

            IEnumerable<string> subdirectories = directoryScanner.GetSubdirectories(sourceDirectory);
            List<string> allDirectories = subdirectories.ToList();
            
            // Add root directory to the list
            allDirectories.Add(sourceDirectory);

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
                // Directory name is used as the ruleset tag for filtering within the unified "rulesets" index
                string rulesetTag = directoryInfo.Name.ToLowerInvariant();
                logger.LogInformation("Processing directory: {Directory} -> Ruleset tag: {RulesetTag}", directory, rulesetTag);

                using Activity? directoryActivity = activitySource.StartActivity("Indexing.ProcessDirectory");
                directoryActivity?.SetTag("indexer.directory", directory);
                directoryActivity?.SetTag("indexer.ruleset_tag", rulesetTag);

                IndexingSummary dirSummary = await ProcessDirectoryAsync(directory, rulesetTag, cancellationToken);
                
                if (directoryActivity != null)
                {
                    directoryActivity.SetTag("indexer.directory_processed", dirSummary.TotalProcessed);
                    directoryActivity.SetTag("indexer.directory_succeeded", dirSummary.TotalSucceeded);
                    directoryActivity.SetTag("indexer.directory_errors", dirSummary.TotalErrors);
                    directoryActivity.SetStatus(dirSummary.TotalErrors > 0 ? ActivityStatusCode.Error : ActivityStatusCode.Ok);
                }
                
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

    private async Task<IndexingSummary> ProcessDirectoryAsync(string directoryPath, string rulesetTag, CancellationToken cancellationToken)
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
                // rulesetTag is used as the "ruleset" tag value for filtering within the unified "rulesets" index
                string id = await documentIndexer.IndexDocumentAsync(filePath, rulesetTag, cancellationToken);
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

