using Microsoft.Extensions.Logging;
using MattEland.Jaimes.Indexer.Configuration;

namespace MattEland.Jaimes.Indexer.Services;

public class IndexingOrchestrator
{
    private readonly ILogger<IndexingOrchestrator> _logger;
    private readonly IDirectoryScanner _directoryScanner;
    private readonly IChangeTracker _changeTracker;
    private readonly IDocumentIndexer _documentIndexer;
    private readonly IndexerOptions _options;

    public IndexingOrchestrator(
        ILogger<IndexingOrchestrator> logger,
        IDirectoryScanner directoryScanner,
        IChangeTracker changeTracker,
        IDocumentIndexer documentIndexer,
        IndexerOptions options)
    {
        _logger = logger;
        _directoryScanner = directoryScanner;
        _changeTracker = changeTracker;
        _documentIndexer = documentIndexer;
        _options = options;
    }

    public async Task<IndexingSummary> ProcessAllDirectoriesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting indexing process for directory: {SourceDirectory}", _options.SourceDirectory);
        
        IndexingSummary summary = new();

        try
        {
            IEnumerable<string> subdirectories = _directoryScanner.GetSubdirectories(_options.SourceDirectory);
            
            foreach (string subdirectory in subdirectories)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Indexing process cancelled");
                    break;
                }

                string indexName = GetIndexName(subdirectory);
                _logger.LogInformation("Processing subdirectory: {Subdirectory} with index: {IndexName}", subdirectory, indexName);

                IndexingSummary dirSummary = await ProcessDirectoryAsync(subdirectory, indexName, cancellationToken);
                summary.Add(dirSummary);
            }

            // Also process files in the root directory
            string rootIndexName = GetIndexName(_options.SourceDirectory);
            _logger.LogInformation("Processing root directory: {SourceDirectory} with index: {IndexName}", _options.SourceDirectory, rootIndexName);
            IndexingSummary rootSummary = await ProcessDirectoryAsync(_options.SourceDirectory, rootIndexName, cancellationToken);
            summary.Add(rootSummary);

            _logger.LogInformation(
                "Indexing complete. Processed: {TotalProcessed}, Added: {TotalAdded}, Updated: {TotalUpdated}, Skipped: {TotalSkipped}, Errors: {TotalErrors}",
                summary.TotalProcessed, summary.TotalAdded, summary.TotalUpdated, summary.TotalSkipped, summary.TotalErrors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during indexing process");
            throw;
        }

        return summary;
    }

    private async Task<IndexingSummary> ProcessDirectoryAsync(string directoryPath, string indexName, CancellationToken cancellationToken)
    {
        IndexingSummary summary = new();
        IEnumerable<string> files = _directoryScanner.GetFiles(directoryPath, _options.SupportedExtensions);

        foreach (string filePath in files)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                IndexingResult result = await ProcessFileAsync(filePath, indexName, cancellationToken);
                summary.TotalProcessed++;

                switch (result)
                {
                    case IndexingResult.Added:
                        summary.TotalAdded++;
                        _logger.LogInformation("Added new document: {FilePath}", filePath);
                        break;
                    case IndexingResult.Updated:
                        summary.TotalUpdated++;
                        _logger.LogInformation("Updated existing document: {FilePath}", filePath);
                        break;
                    case IndexingResult.Skipped:
                        summary.TotalSkipped++;
                        _logger.LogDebug("Skipped unchanged document: {FilePath}", filePath);
                        break;
                    case IndexingResult.Error:
                        summary.TotalErrors++;
                        _logger.LogError("Error processing document: {FilePath}", filePath);
                        break;
                }
            }
            catch (Exception ex)
            {
                summary.TotalErrors++;
                _logger.LogError(ex, "Unexpected error processing file: {FilePath}", filePath);
            }
        }

        return summary;
    }

    private async Task<IndexingResult> ProcessFileAsync(string filePath, string indexName, CancellationToken cancellationToken)
    {
        try
        {
            string currentHash = await _changeTracker.ComputeFileHashAsync(filePath, cancellationToken);
            DocumentState? existingState = await _changeTracker.GetDocumentStateAsync(filePath, cancellationToken);

            if (existingState != null)
            {
                if (existingState.Hash == currentHash)
                {
                    _logger.LogDebug("Document unchanged, skipping: {FilePath}", filePath);
                    return IndexingResult.Skipped;
                }

                // Document has changed, update it
                _logger.LogInformation("Document changed, updating: {FilePath}", filePath);
            }
            else
            {
                _logger.LogInformation("New document found, adding: {FilePath}", filePath);
            }

            bool success = await _documentIndexer.IndexDocumentAsync(filePath, indexName, cancellationToken);
            
            if (success)
            {
                DocumentState newState = new()
                {
                    FilePath = filePath,
                    Hash = currentHash,
                    LastIndexed = DateTime.UtcNow
                };
                await _changeTracker.SaveDocumentStateAsync(newState, cancellationToken);

                return existingState == null ? IndexingResult.Added : IndexingResult.Updated;
            }

            return IndexingResult.Error;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file: {FilePath}", filePath);
            return IndexingResult.Error;
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

    private enum IndexingResult
    {
        Added,
        Updated,
        Skipped,
        Error
    }

    public class IndexingSummary
    {
        public int TotalProcessed { get; set; }
        public int TotalAdded { get; set; }
        public int TotalUpdated { get; set; }
        public int TotalSkipped { get; set; }
        public int TotalErrors { get; set; }

        public void Add(IndexingSummary other)
        {
            TotalProcessed += other.TotalProcessed;
            TotalAdded += other.TotalAdded;
            TotalUpdated += other.TotalUpdated;
            TotalSkipped += other.TotalSkipped;
            TotalErrors += other.TotalErrors;
        }
    }
}

