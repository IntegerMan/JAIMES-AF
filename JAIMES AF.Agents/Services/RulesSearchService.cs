namespace MattEland.Jaimes.Agents.Services;

public class RulesSearchService : IRulesSearchService
{
    private static readonly ActivitySource ActivitySource = new("Jaimes.Agents.RulesSearch");
    private const string DocumentEmbeddingsCollectionName = "document-embeddings";

    private readonly ILogger<RulesSearchService> _logger;
    private readonly IQdrantRulesStore _rulesStore;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly IRagSearchStorageService? _storageService;

    public RulesSearchService(
        ILogger<RulesSearchService> logger,
        IQdrantRulesStore rulesStore,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        IRagSearchStorageService? storageService = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _rulesStore = rulesStore ?? throw new ArgumentNullException(nameof(rulesStore));
        _embeddingGenerator = embeddingGenerator ?? throw new ArgumentNullException(nameof(embeddingGenerator));
        _storageService = storageService;

        _logger.LogInformation("RulesSearchService initialized with Qdrant");
    }

    public async Task<string> SearchRulesAsync(string rulesetId,
        string query,
        bool storeResults = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rulesetId))
            throw new ArgumentException("Ruleset ID cannot be null or empty", nameof(rulesetId));

        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be null or empty", nameof(query));

        _logger.LogInformation("Searching rules for ruleset {RulesetId} with query: {Query}", rulesetId, query);

        // Create OpenTelemetry activity for the overall search operation
        using Activity? activity = ActivitySource.StartActivity("RulesSearch.Search");
        if (activity != null)
        {
            activity.SetTag("ruleset.id", rulesetId);
            activity.SetTag("search.query", query);
            activity.SetTag("search.limit", 5);
        }

        try
        {
            // Generate embedding for the query
            GeneratedEmbeddings<Embedding<float>> embeddings =
                await _embeddingGenerator.GenerateAsync([query], cancellationToken: cancellationToken);
            float[] queryEmbedding = embeddings[0].Vector.ToArray();

            // Search for similar rules in Qdrant
            List<RuleSearchResult> results = await _rulesStore.SearchRulesAsync(
                queryEmbedding,
                rulesetId,
                5,
                cancellationToken);

            if (activity != null)
            {
                activity.SetTag("search.result_count", results.Count);
                activity.SetStatus(ActivityStatusCode.Ok);
            }

            if (results.Count == 0)
            {
                _logger.LogWarning("No results found for query: {Query} in ruleset: {RulesetId}", query, rulesetId);
                return "No relevant rules found for your query.";
            }

            _logger.LogInformation("Found {Count} results for query: {Query} in ruleset: {RulesetId}",
                results.Count,
                query,
                rulesetId);

            // Combine results into a single answer
            List<string> resultTexts = results.Select(r =>
                    string.IsNullOrWhiteSpace(r.Title)
                        ? r.Content
                        : $"{r.Title}\n\n{r.Content}")
                .ToList();

            return string.Join("\n\n---\n\n", resultTexts);
        }
        catch (Exception ex)
        {
            if (activity != null)
            {
                activity.SetTag("error", true);
                activity.SetTag("error.message", ex.Message);
                activity.SetStatus(ActivityStatusCode.Error, ex.Message);
            }

            throw;
        }
    }

    public async Task<SearchRulesResponse> SearchRulesDetailedAsync(string? rulesetId,
        string query,
        bool storeResults = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be null or empty", nameof(query));

        _logger.LogInformation("Searching rules with query: {Query}, rulesetId: {RulesetId}",
            query,
            rulesetId ?? "all");

        // Create OpenTelemetry activity for search operation
        using Activity? activity = ActivitySource.StartActivity("RulesSearch.SearchDetailed");
        if (activity != null)
        {
            activity.SetTag("ruleset.id", rulesetId ?? "all");
            activity.SetTag("search.query", query);
            activity.SetTag("search.limit", 5);
            activity.SetTag("search.scope", string.IsNullOrWhiteSpace(rulesetId) ? "all_rulesets" : "single_ruleset");
        }

        try
        {
            // Generate embedding for the query
            GeneratedEmbeddings<Embedding<float>> embeddings =
                await _embeddingGenerator.GenerateAsync([query], cancellationToken: cancellationToken);
            float[] queryEmbedding = embeddings[0].Vector.ToArray();

            // Search for similar document rules in Qdrant (document-embeddings collection)
            List<DocumentRuleSearchResult> results = await _rulesStore.SearchDocumentRulesAsync(
                queryEmbedding,
                rulesetId,
                5,
                cancellationToken);

            _logger.LogInformation("Search found {Count} results with filter: {Filter}",
                results.Count,
                string.IsNullOrWhiteSpace(rulesetId) ? "none (all rulesets)" : $"rulesetId={rulesetId}");

            if (activity != null)
            {
                activity.SetTag("search.result_count", results.Count);
                activity.SetStatus(ActivityStatusCode.Ok);
            }

            // Convert to response format - results are already sorted by relevancy descending
            SearchRuleResult[] searchResults = results.Select(r => new SearchRuleResult
                {
                    Text = r.Text,
                    DocumentId = r.DocumentId,
                    DocumentName = r.DocumentName,
                    RulesetId = r.RulesetId,
                    EmbeddingId = r.EmbeddingId,
                    ChunkId = r.ChunkId,
                    Relevancy = r.Relevancy
                })
                .ToArray();

            SearchRulesResponse response = new()
            {
                Results = searchResults
            };

            _logger.LogInformation("Search completed - Results: {ResultCount}", results.Count);

            // Store search results asynchronously if requested
            if (storeResults && _storageService != null)
            {
                string? filterJson = null;
                if (!string.IsNullOrWhiteSpace(rulesetId))
                {
                    Dictionary<string, string> filter = new()
                    {
                        {"rulesetId", rulesetId}
                    };
                    filterJson = JsonSerializer.Serialize(filter);
                }

                _storageService.EnqueueSearchResults(
                    query,
                    rulesetId,
                    DocumentEmbeddingsCollectionName,
                    filterJson,
                    searchResults);
            }

            return response;
        }
        catch (Exception ex)
        {
            if (activity != null)
            {
                activity.SetTag("error", true);
                activity.SetTag("error.message", ex.Message);
                activity.SetStatus(ActivityStatusCode.Error, ex.Message);
            }

            throw;
        }
    }

    public async Task IndexRuleAsync(string rulesetId,
        string ruleId,
        string title,
        string content,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rulesetId))
            throw new ArgumentException("Ruleset ID cannot be null or empty", nameof(rulesetId));

        if (string.IsNullOrWhiteSpace(ruleId))
            throw new ArgumentException("Rule ID cannot be null or empty", nameof(ruleId));

        _logger.LogInformation("Indexing rule {RuleId} for ruleset {RulesetId}", ruleId, rulesetId);

        // Create OpenTelemetry activity for indexing operation
        using Activity? activity = ActivitySource.StartActivity("RulesSearch.Index");
        if (activity != null)
        {
            activity.SetTag("ruleset.id", rulesetId);
            activity.SetTag("rule.id", ruleId);
            activity.SetTag("document.title", title);
        }

        try
        {
            // Combine title and content for indexing
            string fullContent = string.IsNullOrWhiteSpace(title)
                ? content
                : $"Title: {title}\n\nContent: {content}";

            // Generate embedding for the rule content
            GeneratedEmbeddings<Embedding<float>> embeddings =
                await _embeddingGenerator.GenerateAsync([fullContent], cancellationToken: cancellationToken);
            float[] embedding = embeddings[0].Vector.ToArray();

            // Prepare metadata
            Dictionary<string, string> metadata = new()
            {
                {"rulesetId", rulesetId},
                {"ruleId", ruleId},
                {"content", content}
            };

            if (!string.IsNullOrWhiteSpace(title)) metadata["title"] = title;

            // Store rule in Qdrant
            await _rulesStore.StoreRuleAsync(ruleId, embedding, metadata, cancellationToken);

            if (activity != null) activity.SetStatus(ActivityStatusCode.Ok);

            _logger.LogInformation("Successfully indexed rule {RuleId} for ruleset {RulesetId}", ruleId, rulesetId);
        }
        catch (Exception ex)
        {
            if (activity != null)
            {
                activity.SetTag("error", true);
                activity.SetTag("error.message", ex.Message);
                activity.SetStatus(ActivityStatusCode.Error, ex.Message);
            }

            throw;
        }
    }

    public async Task EnsureRulesetIndexedAsync(string rulesetId, CancellationToken cancellationToken = default)
    {
        // This method is kept for interface compatibility but rules are now managed
        // directly in Qdrant, not through EF entities
        // Rules should be indexed via IndexRuleAsync when they are added to the system
        _logger.LogInformation("EnsureRulesetIndexedAsync called for ruleset {RulesetId} - rules are stored in Qdrant",
            rulesetId);
        await Task.CompletedTask;
    }
}