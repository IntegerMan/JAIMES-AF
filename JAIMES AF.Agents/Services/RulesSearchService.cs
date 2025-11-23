using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.Agents.Services;

public class RulesSearchService : IRulesSearchService
{
    private static readonly ActivitySource ActivitySource = new("Jaimes.Agents.RulesSearch");
    
    private readonly ILogger<RulesSearchService> _logger;
    private readonly IKernelMemory _memory;

    public RulesSearchService(
        ILogger<RulesSearchService> logger,
        IKernelMemory memory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));

        _logger.LogInformation("RulesSearchService initialized with Kernel Memory");
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

        // Use SearchAsync instead of AskAsync to avoid text generation requirement and version mismatch issues
        // SearchAsync returns SearchResult with results directly without needing a text generator
        string indexName = GetIndexName();
        
        // Create filter to search only within the specified ruleset
        List<MemoryFilter> filters = [new MemoryFilter().ByTag("rulesetId", rulesetId)];
        
        // Create OpenTelemetry activity for the overall search operation
        // Note: HTTP requests to Kernel Memory service are automatically instrumented by HttpClient instrumentation
        using Activity? activity = ActivitySource.StartActivity("RulesSearch.Search");
        if (activity != null)
        {
            activity.SetTag("ruleset.id", rulesetId);
            activity.SetTag("search.index", indexName);
            activity.SetTag("search.query", query);
            activity.SetTag("search.limit", 5);
        }
        
        SearchResult searchResult;
        try
        {
            searchResult = await _memory.SearchAsync(
                query: query,
                index: indexName,
                filters: filters,
                limit: 5, // Limit to top 5 results
                cancellationToken: cancellationToken);
            
            if (activity != null)
            {
                activity.SetTag("search.result_count", searchResult.Results?.Count ?? 0);
                activity.SetStatus(ActivityStatusCode.Ok);
            }
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

        List<string> resultTexts = [];
        if (searchResult.Results != null)
        {
            foreach (Citation citation in searchResult.Results)
            {
                if (citation.Partitions != null && citation.Partitions.Count > 0)
                {
                    foreach (Citation.Partition partition in citation.Partitions)
                    {
                        if (!string.IsNullOrWhiteSpace(partition.Text))
                        {
                            resultTexts.Add(partition.Text);
                        }
                    }
                }
            }
        }

        if (resultTexts.Count == 0)
        {
            _logger.LogWarning("No results found for query: {Query} in ruleset: {RulesetId}", query, rulesetId);
            return "No relevant rules found for your query.";
        }

        _logger.LogInformation("Found {Count} results for query: {Query} in ruleset: {RulesetId}", resultTexts.Count, query, rulesetId);
        // Combine results into a single answer
        return string.Join("\n\n---\n\n", resultTexts);
    }

    public async Task<SearchRulesResponse> SearchRulesDetailedAsync(string? rulesetId, string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Query cannot be null or empty", nameof(query));
        }

        _logger.LogInformation("Searching rules with query: {Query}, rulesetId: {RulesetId}", query, rulesetId ?? "all");

        string indexName = GetIndexName();
        List<MemoryFilter>? filters = null;
        
        // Create filter if searching within a specific ruleset
        if (!string.IsNullOrWhiteSpace(rulesetId))
        {
            filters = [new MemoryFilter().ByTag("rulesetId", rulesetId)];
        }
        
        // Create OpenTelemetry activity for search operation
        using Activity? activity = ActivitySource.StartActivity("RulesSearch.SearchDetailed");
        if (activity != null)
        {
            activity.SetTag("http.method", "POST");
            activity.SetTag("http.route", "/memory/search");
            activity.SetTag("ruleset.id", rulesetId ?? "all");
            activity.SetTag("search.index", indexName);
            activity.SetTag("search.query", query);
            activity.SetTag("search.limit", 10);
            activity.SetTag("search.scope", string.IsNullOrWhiteSpace(rulesetId) ? "all_rulesets" : "single_ruleset");
        }
        
        SearchResult searchResult;
        try
        {
            // Search the unified rulesets index, with optional filtering by rulesetId tag
            // Note: HTTP requests to Kernel Memory service are automatically instrumented by HttpClient instrumentation
            searchResult = await _memory.SearchAsync(
                query: query,
                index: indexName,
                filters: filters,
                limit: 10, // Limit to top 10 results
                cancellationToken: cancellationToken);
            
            _logger.LogInformation("Search found {Count} results in index: {IndexName} with filter: {Filter}", 
                searchResult.Results?.Count ?? 0, indexName, 
                string.IsNullOrWhiteSpace(rulesetId) ? "none (all rulesets)" : $"rulesetId={rulesetId}");
            
            if (activity != null)
            {
                activity.SetTag("search.result_count", searchResult.Results?.Count ?? 0);
                activity.SetStatus(ActivityStatusCode.Ok);
            }
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
        
        // Convert SearchResult citations to response format
        List<CitationInfo> citations = [];
        List<DocumentInfo> documents = [];
        HashSet<string> seenDocumentIds = [];
        List<string> answerTexts = [];
        
        if (searchResult.Results != null)
        {
            foreach (Citation citation in searchResult.Results)
            {
                // Extract text from partitions
                string text = string.Empty;
                if (citation.Partitions != null && citation.Partitions.Count > 0)
                {
                    List<string> partitionTexts = citation.Partitions
                        .Where(p => !string.IsNullOrWhiteSpace(p.Text))
                        .Select(p => p.Text!)
                        .ToList();
                    
                    text = string.Join("\n\n---\n\n", partitionTexts);
                    answerTexts.AddRange(partitionTexts);
                }
                
                // Build citation
                string documentId = citation.DocumentId ?? "Unknown";
                string source = BuildSourceString(citation);
                
                citations.Add(new CitationInfo
                {
                    Source = source,
                    Text = text,
                    Relevance = null // Citation doesn't expose relevance directly
                });
                
                // Build document info (only once per document)
                if (!seenDocumentIds.Contains(documentId))
                {
                    seenDocumentIds.Add(documentId);
                    
                    Dictionary<string, string> tags = new();
                    // Citation doesn't expose Tags directly, but we can extract from metadata if available
                    // For now, we'll leave tags empty as Kernel Memory's Citation structure doesn't expose them directly
                    
                    documents.Add(new DocumentInfo
                    {
                        DocumentId = documentId,
                        Index = citation.Index ?? indexName,
                        Tags = tags
                    });
                }
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
            "Search completed - Index: {IndexName}, Results: {ResultCount}, Citations: {CitationCount}, Documents: {DocumentCount}", 
            indexName, searchResult.Results?.Count ?? 0, citations.Count, documents.Count);
        
        if (citations.Count > 0)
        {
            _logger.LogInformation("Citation sources found: {Sources}", 
                string.Join(", ", citations.Select(c => c.Source)));
        }
        else
        {
            _logger.LogWarning("No citations found in search results for query: {Query} in index: {IndexName}", 
                query, indexName);
        }
        
        return response;
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

        // Create a document ID that includes the rule ID
        string documentId = $"rule-{ruleId}";
        string indexName = GetIndexName();
        
        // Combine title and content for indexing
        string fullContent = $"Title: {title}\n\nContent: {content}";

        // Create OpenTelemetry activity for indexing operation
        using Activity? activity = ActivitySource.StartActivity("RulesSearch.Index");
        if (activity != null)
        {
            activity.SetTag("http.method", "POST");
            activity.SetTag("http.route", "/memory/documents");
            activity.SetTag("ruleset.id", rulesetId);
            activity.SetTag("rule.id", ruleId);
            activity.SetTag("document.id", documentId);
            activity.SetTag("search.index", indexName);
            activity.SetTag("document.title", title);
        }

        try
        {
            // Index the rule in the unified rulesets index with tags for filtering
            await _memory.ImportTextAsync(
                text: fullContent,
                documentId: documentId,
                index: indexName,
                tags: new TagCollection
                {
                    { "rulesetId", rulesetId },
                    { "ruleId", ruleId },
                    { "title", title }
                },
                cancellationToken: cancellationToken);

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
        // directly in the vector store, not through EF entities
        // Rules should be indexed via IndexRuleAsync when they are added to the system
        _logger.LogInformation("EnsureRulesetIndexedAsync called for ruleset {RulesetId} - rules are stored in vector database", rulesetId);
        await Task.CompletedTask;
    }

    private static string GetIndexName()
    {
        // All rulesets are stored in a single "rulesets" index
        // Filtering by specific ruleset is done via tags
        return "rulesets";
    }

    private static string BuildSourceString(Citation citation)
    {
        // Try to build a descriptive source string
        // Priority: DocumentId (which may contain filename), then SourceUrl, then Link, then Index
        string source = citation.DocumentId ?? string.Empty;
        
        // If DocumentId looks like a filename (contains extension or path info), use it
        if (!string.IsNullOrWhiteSpace(source) && (source.Contains('.') || source.Contains('-')))
        {
            // For indexed documents, DocumentId format is: "rulesetId-filename.ext"
            // Extract just the filename part for better readability
            int lastDashIndex = source.LastIndexOf('-');
            if (lastDashIndex >= 0 && lastDashIndex < source.Length - 1)
            {
                string filename = source.Substring(lastDashIndex + 1);
                if (!string.IsNullOrWhiteSpace(filename))
                {
                    return filename;
                }
            }
            return source;
        }
        
        // Fallback to other available properties
        if (!string.IsNullOrWhiteSpace(citation.SourceUrl))
        {
            return citation.SourceUrl;
        }
        
        if (!string.IsNullOrWhiteSpace(citation.Link))
        {
            return citation.Link;
        }
        
        if (!string.IsNullOrWhiteSpace(citation.Index))
        {
            return $"Index: {citation.Index}";
        }
        
        // Last resort: use DocumentId even if it doesn't look like a filename
        return !string.IsNullOrWhiteSpace(source) ? source : "Unknown Source";
    }

}

