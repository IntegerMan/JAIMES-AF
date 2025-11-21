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

        // Use Kernel Memory's Ask method to search for relevant rules
        // Use the same index format as the Indexer (just the ruleset ID in lowercase)
        MemoryAnswer answer = await _memory.AskAsync(
            question: query,
            index: GetIndexName(rulesetId),
            filters: null, // Index name is sufficient for filtering
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
        string? indexName = null;
        
        if (string.IsNullOrWhiteSpace(rulesetId))
        {
            // Search across all rulesets - use null to search all indexes
            // Kernel Memory searches across all indexes when index is null
            // Note: AskAsync may not work well for cross-index searches - if this fails, consider
            // using SearchAsync or listing indexes and searching them individually
            _logger.LogInformation("Searching across all indexes (no specific ruleset)");
            answer = await _memory.AskAsync(
                question: query,
                index: null, // null searches across all indexes
                filters: null,
                cancellationToken: cancellationToken);
        }
        else
        {
            // Search within a specific ruleset using the same index format as the Indexer
            indexName = GetIndexName(rulesetId);
            _logger.LogInformation("Searching in index: {IndexName} for ruleset: {RulesetId}", indexName, rulesetId);
            answer = await _memory.AskAsync(
                question: query,
                index: indexName,
                filters: null, // Index name is sufficient for filtering
                cancellationToken: cancellationToken);
        }
        
        _logger.LogInformation("AskAsync completed - Index: {IndexName}, Answer: {AnswerPreview}, RelevantSources: {SourceCount}", 
            indexName ?? "(all indexes)",
            string.IsNullOrWhiteSpace(answer.Result) ? "(empty)" : answer.Result.Length > 50 ? answer.Result.Substring(0, 50) + "..." : answer.Result,
            answer.RelevantSources?.Count ?? 0);
        
        // Log detailed information about sources for debugging
        if (answer.RelevantSources != null && answer.RelevantSources.Count > 0)
        {
            _logger.LogDebug("Relevant sources details: {Sources}", 
                string.Join(", ", answer.RelevantSources.Select(s => $"DocId: {s.DocumentId}, Index: {s.Index}")));
        }
        else
        {
            _logger.LogWarning("No relevant sources found for query: {Query} in index: {IndexName}", 
                query, indexName ?? "(all indexes)");
        }

        // Extract citations from the answer with source and matched text
        // Always extract citations even if the answer is empty or "INFO NOT FOUND"
        List<CitationInfo> citations = [];
        if (answer.RelevantSources != null && answer.RelevantSources.Count > 0)
        {
            _logger.LogInformation("Found {Count} relevant sources in answer", answer.RelevantSources.Count);
            
            foreach (Citation citation in answer.RelevantSources)
            {
                // Build a descriptive source string
                string source = BuildSourceString(citation);
                
                // Extract matched text from partitions
                // Get all partitions or the first one if available
                string text = string.Empty;
                if (citation.Partitions != null && citation.Partitions.Count > 0)
                {
                    // Combine all partition texts, or use the first one if there's only one
                    if (citation.Partitions.Count == 1)
                    {
                        text = citation.Partitions[0].Text ?? string.Empty;
                    }
                    else
                    {
                        // Combine multiple partitions with separators
                        text = string.Join("\n\n---\n\n", 
                            citation.Partitions.Select(p => p.Text ?? string.Empty).Where(t => !string.IsNullOrWhiteSpace(t)));
                    }
                }
                
                _logger.LogDebug("Extracted citation - Source: {Source}, TextLength: {TextLength}, Partitions: {PartitionCount}", 
                    source, text.Length, citation.Partitions?.Count ?? 0);
                
                citations.Add(new CitationInfo
                {
                    Source = source,
                    Text = text,
                    Relevance = null // Citation doesn't have a Relevance property in Kernel Memory
                });
            }
        }
        else
        {
            _logger.LogWarning("No RelevantSources found in MemoryAnswer. Answer.Result: {AnswerResult}", 
                answer.Result ?? "(null)");
        }

        // Extract document information
        List<DocumentInfo> documents = [];
        HashSet<string> seenDocumentIds = [];
        
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

        // Build response - always include citations and documents even if answer is empty or "INFO NOT FOUND"
        // This allows users to see what sources were found even if no answer was generated
        string answerText = answer.Result ?? string.Empty;
        bool hasValidAnswer = !string.IsNullOrWhiteSpace(answerText) && 
                              !answerText.Equals("INFO NOT FOUND", StringComparison.OrdinalIgnoreCase);
        
        if (!hasValidAnswer)
        {
            if (citations.Count > 0 || documents.Count > 0)
            {
                // We have sources but no answer - this is useful information
                answerText = "No answer was generated, but relevant sources were found. See citations and documents below.";
            }
            else
            {
                // No sources and no answer
                answerText = "No relevant rules found for your query. No matching documents or citations were found.";
            }
        }

        SearchRulesResponse response = new()
        {
            Answer = answerText,
            Citations = citations.ToArray(),
            Documents = documents.ToArray()
        };

        _logger.LogInformation(
            "Search completed - HasValidAnswer: {HasAnswer}, Citations: {CitationCount}, Documents: {DocumentCount}", 
            hasValidAnswer, citations.Count, documents.Count);
        
        if (citations.Count > 0)
        {
            _logger.LogInformation("Citation sources found: {Sources}", 
                string.Join(", ", citations.Select(c => c.Source)));
        }
        else
        {
            _logger.LogWarning("No citations found in search results. RelevantSources was {IsNull}", 
                answer.RelevantSources == null ? "null" : $"not null (count: {answer.RelevantSources.Count})");
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
        // Use the same index name format as the Indexer
        // The Indexer uses just the directory name (ruleset ID) in lowercase
        return rulesetId.ToLowerInvariant();
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

