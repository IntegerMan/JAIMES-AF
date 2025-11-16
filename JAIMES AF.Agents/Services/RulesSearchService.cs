using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using MattEland.Jaimes.ServiceDefinitions.Services;

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

