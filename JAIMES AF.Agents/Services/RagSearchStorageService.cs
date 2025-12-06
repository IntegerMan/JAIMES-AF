using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MattEland.Jaimes.Agents.Services;

public class RagSearchStorageService(
    IServiceProvider serviceProvider,
    ILogger<RagSearchStorageService> logger)
    : BackgroundService, IRagSearchStorageService
{
    private readonly Channel<SearchStorageItem> _queue = Channel.CreateUnbounded<SearchStorageItem>();
    private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly ILogger<RagSearchStorageService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public void EnqueueSearchResults(string query,
        string? rulesetId,
        string indexName,
        string? filterJson,
        SearchRuleResult[] results)
    {
        SearchStorageItem item = new()
        {
            Query = query,
            RulesetId = rulesetId,
            IndexName = indexName,
            FilterJson = filterJson,
            Results = results
        };

        if (!_queue.Writer.TryWrite(item))
            _logger.LogWarning("Failed to enqueue search results for storage. Queue may be closed.");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RagSearchStorageService background worker started");

        await foreach (SearchStorageItem item in _queue.Reader.ReadAllAsync(stoppingToken))
            try
            {
                await StoreSearchResultsAsync(item, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing search results for query: {Query}", item.Query);
            }

        _logger.LogInformation("RagSearchStorageService background worker stopped");
    }

    private async Task StoreSearchResultsAsync(SearchStorageItem item, CancellationToken cancellationToken)
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        JaimesDbContext context = scope.ServiceProvider.GetRequiredService<JaimesDbContext>();

        RagSearchQuery searchQuery = new()
        {
            Id = Guid.NewGuid(),
            Query = item.Query,
            RulesetId = item.RulesetId,
            IndexName = item.IndexName,
            FilterJson = item.FilterJson,
            CreatedAt = DateTime.UtcNow
        };

        context.RagSearchQueries.Add(searchQuery);

        foreach (SearchRuleResult result in item.Results)
        {
            RagSearchResultChunk chunk = new()
            {
                Id = Guid.NewGuid(),
                RagSearchQueryId = searchQuery.Id,
                ChunkId = result.ChunkId,
                DocumentId = result.DocumentId,
                DocumentName = result.DocumentName,
                EmbeddingId = result.EmbeddingId,
                RulesetId = result.RulesetId,
                Relevancy = result.Relevancy
            };

            context.RagSearchResultChunks.Add(chunk);
        }

        await context.SaveChangesAsync(cancellationToken);
        _logger.LogDebug("Stored search query {QueryId} with {ChunkCount} result chunks",
            searchQuery.Id,
            item.Results.Length);
    }

    private sealed class SearchStorageItem
    {
        public required string Query { get; init; }
        public string? RulesetId { get; init; }
        public required string IndexName { get; init; }
        public string? FilterJson { get; init; }
        public required SearchRuleResult[] Results { get; init; }
    }
}