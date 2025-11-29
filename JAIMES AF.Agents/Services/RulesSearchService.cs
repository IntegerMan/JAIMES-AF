using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.AI;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.Agents.Services;

public class RulesSearchService : IRulesSearchService
{
    private static readonly ActivitySource ActivitySource = new("Jaimes.Agents.RulesSearch");
    
    private readonly ILogger<RulesSearchService> _logger;
    private readonly IQdrantRulesStore _rulesStore;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;

    public RulesSearchService(
        ILogger<RulesSearchService> logger,
        IQdrantRulesStore rulesStore,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _rulesStore = rulesStore ?? throw new ArgumentNullException(nameof(rulesStore));
        _embeddingGenerator = embeddingGenerator ?? throw new ArgumentNullException(nameof(embeddingGenerator));

        _logger.LogInformation("RulesSearchService initialized with Qdrant");
    }

    public async Task<string> SearchRulesAsync(string rulesetId, string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rulesetId))
        {
            throw new ArgumentException("Ruleset ID cannot be null or empty", nameof(rulesetId));
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Query cannot be null or empty", nameof(query));
        }

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
            GeneratedEmbeddings<Embedding<float>> embeddings = await _embeddingGenerator.GenerateAsync([query], cancellationToken: cancellationToken);
            float[] queryEmbedding = embeddings[0].Vector.ToArray();
            
            // Search for similar rules in Qdrant
            List<RuleSearchResult> results = await _rulesStore.SearchRulesAsync(
                queryEmbedding,
                rulesetId,
                limit: 5,
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

            _logger.LogInformation("Found {Count} results for query: {Query} in ruleset: {RulesetId}", results.Count, query, rulesetId);
            
            // Combine results into a single answer
            List<string> resultTexts = results.Select(r => 
                string.IsNullOrWhiteSpace(r.Title) 
                    ? r.Content 
                    : $"{r.Title}\n\n{r.Content}").ToList();
            
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

    public async Task<SearchRulesResponse> SearchRulesDetailedAsync(string? rulesetId, string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Query cannot be null or empty", nameof(query));
        }

        _logger.LogInformation("Searching rules with query: {Query}, rulesetId: {RulesetId}", query, rulesetId ?? "all");
        
        // Create OpenTelemetry activity for search operation
        using Activity? activity = ActivitySource.StartActivity("RulesSearch.SearchDetailed");
        if (activity != null)
        {
            activity.SetTag("ruleset.id", rulesetId ?? "all");
            activity.SetTag("search.query", query);
            activity.SetTag("search.limit", 10);
            activity.SetTag("search.scope", string.IsNullOrWhiteSpace(rulesetId) ? "all_rulesets" : "single_ruleset");
        }
        
        try
        {
            // Generate embedding for the query
            GeneratedEmbeddings<Embedding<float>> embeddings = await _embeddingGenerator.GenerateAsync([query], cancellationToken: cancellationToken);
            float[] queryEmbedding = embeddings[0].Vector.ToArray();
            
            // Search for similar rules in Qdrant
            List<RuleSearchResult> results = await _rulesStore.SearchRulesAsync(
                queryEmbedding,
                rulesetId,
                limit: 10,
                cancellationToken);
            
            _logger.LogInformation("Search found {Count} results with filter: {Filter}", 
                results.Count, 
                string.IsNullOrWhiteSpace(rulesetId) ? "none (all rulesets)" : $"rulesetId={rulesetId}");
            
            if (activity != null)
            {
                activity.SetTag("search.result_count", results.Count);
                activity.SetStatus(ActivityStatusCode.Ok);
            }
            
            // Convert results to response format
            List<CitationInfo> citations = [];
            List<DocumentInfo> documents = [];
            HashSet<string> seenRuleIds = [];
            List<string> answerTexts = [];
            
            foreach (RuleSearchResult result in results)
            {
                // Build citation
                string source = string.IsNullOrWhiteSpace(result.Title) 
                    ? $"Rule {result.RuleId}" 
                    : result.Title;
                
                string text = string.IsNullOrWhiteSpace(result.Title) 
                    ? result.Content 
                    : $"{result.Title}\n\n{result.Content}";
                
                citations.Add(new CitationInfo
                {
                    Source = source,
                    Text = text,
                    Relevance = result.Score
                });
                
                answerTexts.Add(text);
                
                // Build document info (only once per rule)
                if (!seenRuleIds.Contains(result.RuleId))
                {
                    seenRuleIds.Add(result.RuleId);
                    
                    Dictionary<string, string> tags = new()
                    {
                        { "rulesetId", result.RulesetId },
                        { "ruleId", result.RuleId }
                    };
                    
                    if (!string.IsNullOrWhiteSpace(result.Title))
                    {
                        tags["title"] = result.Title;
                    }
                    
                    documents.Add(new DocumentInfo
                    {
                        DocumentId = result.RuleId,
                        Index = "rulesets",
                        Tags = tags
                    });
                }
            }
            
            // Build answer text from results
            string answerText;
            if (answerTexts.Count > 0)
            {
                answerText = string.Join("\n\n---\n\n", answerTexts);
            }
            else if (citations.Count > 0 || documents.Count > 0)
            {
                answerText = "Relevant sources were found, but no text content was available. See citations and documents below.";
            }
            else
            {
                answerText = "No relevant rules found for your query. No matching documents or citations were found.";
            }

            SearchRulesResponse response = new()
            {
                Answer = answerText,
                Citations = citations.ToArray(),
                Documents = documents.ToArray()
            };

            _logger.LogInformation(
                "Search completed - Results: {ResultCount}, Citations: {CitationCount}, Documents: {DocumentCount}", 
                results.Count, citations.Count, documents.Count);
            
            if (citations.Count > 0)
            {
                _logger.LogInformation("Citation sources found: {Sources}", 
                    string.Join(", ", citations.Select(c => c.Source)));
            }
            else
            {
                _logger.LogWarning("No citations found in search results for query: {Query}", query);
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

    public async Task IndexRuleAsync(string rulesetId, string ruleId, string title, string content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rulesetId))
        {
            throw new ArgumentException("Ruleset ID cannot be null or empty", nameof(rulesetId));
        }

        if (string.IsNullOrWhiteSpace(ruleId))
        {
            throw new ArgumentException("Rule ID cannot be null or empty", nameof(ruleId));
        }

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
            GeneratedEmbeddings<Embedding<float>> embeddings = await _embeddingGenerator.GenerateAsync([fullContent], cancellationToken: cancellationToken);
            float[] embedding = embeddings[0].Vector.ToArray();

            // Prepare metadata
            Dictionary<string, string> metadata = new()
            {
                { "rulesetId", rulesetId },
                { "ruleId", ruleId },
                { "content", content }
            };
            
            if (!string.IsNullOrWhiteSpace(title))
            {
                metadata["title"] = title;
            }

            // Store rule in Qdrant
            await _rulesStore.StoreRuleAsync(ruleId, embedding, metadata, cancellationToken);

            if (activity != null)
            {
                activity.SetStatus(ActivityStatusCode.Ok);
            }

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
        _logger.LogInformation("EnsureRulesetIndexedAsync called for ruleset {RulesetId} - rules are stored in Qdrant", rulesetId);
        await Task.CompletedTask;
    }
}
