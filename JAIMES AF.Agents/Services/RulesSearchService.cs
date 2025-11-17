using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.Agents.Services;

public class RulesSearchService : IRulesSearchService
{
    private readonly ILogger<RulesSearchService> _logger;
    private readonly IKernelMemory _memory;

    public RulesSearchService(
        ILogger<RulesSearchService> logger,
        JaimesChatOptions chatOptions)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Configure Kernel Memory with Azure OpenAI
        // Reference: https://blog.leadingedje.com/post/ai/documents/kernelmemory.html
        OpenAIConfig openAiConfig = new()
        {
            APIKey = chatOptions.ApiKey,
            Endpoint = chatOptions.Endpoint,
            TextModel = chatOptions.Deployment,
            EmbeddingModel = chatOptions.Deployment // Use the same deployment for embeddings, or configure separately if needed
        };

        // Use SQLite as the vector store for Kernel Memory
        // Kernel Memory's WithSimpleVectorDb uses SQLite for vector storage
        // Reference: https://blog.leadingedje.com/post/ai/documents/kernelmemory.html
        string vectorDbConnectionString = "Data Source=km_vector_store.db";

        _memory = new KernelMemoryBuilder()
            .WithOpenAI(openAiConfig)
            .WithSimpleVectorDb(vectorDbConnectionString)
            .Build();

        _logger.LogInformation("RulesSearchService initialized with Kernel Memory using SQLite vector store");
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

        // Use Kernel Memory's Ask method to search for relevant rules
        // Filter by ruleset ID using tags - the rulesetId is used as an index/filter
        MemoryAnswer answer = await _memory.AskAsync(
            question: query,
            index: GetIndexName(rulesetId),
            filters: new List<MemoryFilter> { new MemoryFilter().ByTag("rulesetId", rulesetId) },
            cancellationToken: cancellationToken);

        if (string.IsNullOrWhiteSpace(answer.Result))
        {
            _logger.LogWarning("No results found for query: {Query} in ruleset: {RulesetId}", query, rulesetId);
            return "No relevant rules found for your query.";
        }

        _logger.LogInformation("Found answer for query: {Query} in ruleset: {RulesetId}", query, rulesetId);
        return answer.Result;
    }

    public async Task<SearchRulesResponse> SearchRulesDetailedAsync(string? rulesetId, string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Query cannot be null or empty", nameof(query));
        }

        _logger.LogInformation("Searching rules with query: {Query}, rulesetId: {RulesetId}", query, rulesetId ?? "all");

        MemoryAnswer answer;
        
        if (string.IsNullOrWhiteSpace(rulesetId))
        {
            // Search across all rulesets - use a wildcard or search all indexes
            // For now, we'll search without a specific index filter
            // Note: This may need adjustment based on Kernel Memory's capabilities
            answer = await _memory.AskAsync(
                question: query,
                index: null, // Search across all indexes
                filters: null,
                cancellationToken: cancellationToken);
        }
        else
        {
            // Search within a specific ruleset
            answer = await _memory.AskAsync(
                question: query,
                index: GetIndexName(rulesetId),
                filters: new List<MemoryFilter> { new MemoryFilter().ByTag("rulesetId", rulesetId) },
                cancellationToken: cancellationToken);
        }

        // Extract citations from the answer
        List<CitationInfo> citations = new();
        if (answer.RelevantSources != null)
        {
            foreach (Citation citation in answer.RelevantSources)
            {
                string source = citation.SourceUrl ?? citation.Link ?? citation.Index ?? "Unknown";
                string text = string.Empty;
                
                if (citation.Partitions != null && citation.Partitions.Count > 0)
                {
                    text = citation.Partitions[0].Text ?? string.Empty;
                }
                
                citations.Add(new CitationInfo
                {
                    Source = source,
                    Text = text,
                    Relevance = null // Citation doesn't have a Relevance property in Kernel Memory
                });
            }
        }

        // Extract document information
        List<DocumentInfo> documents = new();
        HashSet<string> seenDocumentIds = new();
        
        if (answer.RelevantSources != null)
        {
            foreach (Citation citation in answer.RelevantSources)
            {
                string documentId = citation.DocumentId ?? "Unknown";
                if (!seenDocumentIds.Contains(documentId))
                {
                    seenDocumentIds.Add(documentId);
                    
                    Dictionary<string, string> tags = new();
                    // Citation doesn't expose Tags directly, but we can extract from metadata if available
                    // For now, we'll leave tags empty as Kernel Memory's Citation structure doesn't expose them directly
                    
                    documents.Add(new DocumentInfo
                    {
                        DocumentId = documentId,
                        Index = citation.Index ?? "Unknown",
                        Tags = tags
                    });
                }
            }
        }

        SearchRulesResponse response = new()
        {
            Answer = answer.Result ?? "No relevant rules found for your query.",
            Citations = citations.ToArray(),
            Documents = documents.ToArray()
        };

        _logger.LogInformation("Found answer with {CitationCount} citations and {DocumentCount} documents", citations.Count, documents.Count);
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
        
        // Combine title and content for indexing
        string fullContent = $"Title: {title}\n\nContent: {content}";

        // Index the rule with tags for filtering - rulesetId is used as an index
        await _memory.ImportTextAsync(
            text: fullContent,
            documentId: documentId,
            index: GetIndexName(rulesetId),
            tags: new TagCollection
            {
                { "rulesetId", rulesetId },
                { "ruleId", ruleId },
                { "title", title }
            },
            cancellationToken: cancellationToken);

        _logger.LogInformation("Successfully indexed rule {RuleId} for ruleset {RulesetId}", ruleId, rulesetId);
    }

    public async Task EnsureRulesetIndexedAsync(string rulesetId, CancellationToken cancellationToken = default)
    {
        // This method is kept for interface compatibility but rules are now managed
        // directly in the vector store, not through EF entities
        // Rules should be indexed via IndexRuleAsync when they are added to the system
        _logger.LogInformation("EnsureRulesetIndexedAsync called for ruleset {RulesetId} - rules are stored in vector database", rulesetId);
        await Task.CompletedTask;
    }

    private static string GetIndexName(string rulesetId)
    {
        // Use a consistent index name format
        // Kernel Memory uses indexes to organize documents
        return $"ruleset-{rulesetId.ToLowerInvariant()}";
    }
}

