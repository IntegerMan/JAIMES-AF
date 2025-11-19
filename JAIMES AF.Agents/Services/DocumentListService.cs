using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.Agents.Services;

public class DocumentListService : IDocumentListService
{
    private readonly ILogger<DocumentListService> _logger;
    private readonly IKernelMemory _memory;

    public DocumentListService(
        ILogger<DocumentListService> logger,
        IKernelMemory memory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));

        _logger.LogInformation("DocumentListService initialized with Kernel Memory");
    }

    public async Task<IndexListResponse> ListIndexesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Listing all indexes");

        try
        {
            IEnumerable<IndexDetails> indexDetails = await _memory.ListIndexesAsync(cancellationToken);
            List<string> indexes = indexDetails.Select(idx => idx.Name).ToList();
            
            _logger.LogInformation("Found {Count} indexes", indexes.Count);
            
            return new IndexListResponse
            {
                Indexes = indexes.ToArray()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing indexes");
            throw;
        }
    }

    public async Task<DocumentListResponse> ListDocumentsAsync(string? indexName, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Listing documents for index: {IndexName}", indexName ?? "all");

        try
        {
            List<IndexedDocumentInfo> documents = new();

            if (string.IsNullOrWhiteSpace(indexName))
            {
                // List documents from all indexes
                IEnumerable<IndexDetails> indexDetails = await _memory.ListIndexesAsync(cancellationToken);
                List<string> indexes = indexDetails.Select(idx => idx.Name).ToList();
                
                foreach (string index in indexes)
                {
                    await AddDocumentsFromIndexAsync(index, documents, cancellationToken);
                }
            }
            else
            {
                // List documents from specific index
                await AddDocumentsFromIndexAsync(indexName, documents, cancellationToken);
            }

            _logger.LogInformation("Found {Count} documents in index: {IndexName}", documents.Count, indexName ?? "all");

            return new DocumentListResponse
            {
                IndexName = indexName ?? "all",
                Documents = documents.ToArray()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing documents for index: {IndexName}", indexName);
            throw;
        }
    }

    private async Task AddDocumentsFromIndexAsync(string indexName, List<IndexedDocumentInfo> documents, CancellationToken cancellationToken)
    {
        try
        {
            // Note: KernelMemory doesn't have a direct ListDocumentsAsync method
            // We'll use multiple generic search queries to discover documents
            // This is a workaround - we try several common words that should match most documents
            
            _logger.LogInformation("Listing documents from index {IndexName} - using search-based discovery", indexName);
            
            // Try multiple generic queries to find documents
            // These queries should match most document content
            string[] genericQueries = ["the", "document", "text", "content", "information", "data"];
            HashSet<string> seenDocumentIds = new();
            
            foreach (string query in genericQueries)
            {
                try
                {
                    MemoryAnswer answer = await _memory.AskAsync(
                        question: query,
                        index: indexName,
                        filters: null,
                        cancellationToken: cancellationToken);

                    // Extract unique document IDs from citations
                    if (answer.RelevantSources != null)
                    {
                        foreach (Citation citation in answer.RelevantSources)
                        {
                            string? documentId = citation.DocumentId;
                            if (!string.IsNullOrWhiteSpace(documentId) && !seenDocumentIds.Contains(documentId))
                            {
                                seenDocumentIds.Add(documentId);
                                
                                // Get document status to get more details
                                DataPipelineStatus? status = null;
                                try
                                {
                                    status = await _memory.GetDocumentStatusAsync(documentId, indexName, cancellationToken);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Error getting status for document {DocumentId} in index {IndexName}", documentId, indexName);
                                }

                                // Extract tags from document status if available
                                Dictionary<string, string> tags = new();
                                if (status?.Tags != null)
                                {
                                    foreach (KeyValuePair<string, List<string?>> tag in status.Tags)
                                    {
                                        if (tag.Value.Count > 0 && !string.IsNullOrWhiteSpace(tag.Value[0]))
                                        {
                                            tags[tag.Key] = tag.Value[0];
                                        }
                                    }
                                }

                                documents.Add(new IndexedDocumentInfo
                                {
                                    DocumentId = documentId,
                                    Index = indexName,
                                    Tags = tags,
                                    LastUpdate = status != null && status.LastUpdate != default ? status.LastUpdate.DateTime : null,
                                    Status = status?.Completed == true ? "Completed" : status?.Completed == false ? "Processing" : "Unknown"
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Query '{Query}' did not return results for index {IndexName}", query, indexName);
                    // Continue with next query
                }
            }
            
            _logger.LogInformation("Found {Count} unique documents in index {IndexName} using search-based discovery", documents.Count, indexName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error listing documents from index: {IndexName}", indexName);
            // Continue with other indexes even if one fails
        }
    }
}

