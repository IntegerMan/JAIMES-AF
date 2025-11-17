using Microsoft.Extensions.Logging;
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
        logger.LogInformation("Starting indexing process for directory: {SourceDirectory}", options.SourceDirectory);
        
        IndexingSummary summary = new();

        try
        {
            IEnumerable<string> subdirectories = directoryScanner.GetSubdirectories(options.SourceDirectory);
            
            foreach (string subdirectory in subdirectories)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    logger.LogWarning("Indexing process cancelled");
                    break;
                }

                string indexName = GetIndexName(subdirectory);
                logger.LogInformation("Processing subdirectory: {Subdirectory} with index: {IndexName}", subdirectory, indexName);

                IndexingSummary dirSummary = await ProcessDirectoryAsync(subdirectory, indexName, cancellationToken);
                summary.Add(dirSummary);
            }

            // Also process files in the root directory
            string rootIndexName = GetIndexName(options.SourceDirectory);
            logger.LogInformation("Processing root directory: {SourceDirectory} with index: {IndexName}", options.SourceDirectory, rootIndexName);
            IndexingSummary rootSummary = await ProcessDirectoryAsync(options.SourceDirectory, rootIndexName, cancellationToken);
            summary.Add(rootSummary);

            logger.LogInformation(
                "Indexing complete. Processed: {TotalProcessed}, Succeeded: {TotalSucceeded}, Errors: {TotalErrors}",
                summary.TotalProcessed, summary.TotalSucceeded, summary.TotalErrors);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during indexing process");
            throw;
        }

        return summary;
    }

    private async Task<IndexingSummary> ProcessDirectoryAsync(string directoryPath, string indexName, CancellationToken cancellationToken)
    {
        IndexingSummary summary = new();
        IEnumerable<string> files = directoryScanner.GetFiles(directoryPath, options.SupportedExtensions);

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

