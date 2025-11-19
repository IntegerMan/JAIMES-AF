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
            // We'll use a search query to find documents, or we can try to get document status
            // For now, we'll use a broad search query to discover documents
            // This is a workaround - in a production system, you might want to maintain a separate index
            // of document IDs, or use the vector store's native listing capabilities if available
            
            _logger.LogWarning("Listing documents from index {IndexName} - using search-based discovery which may not find all documents", indexName);
            
            // Use a very broad search to discover documents
            // This is not ideal but works with the available API
            try
            {
                MemoryAnswer answer = await _memory.AskAsync(
                    question: "*",
                    index: indexName,
                    filters: null,
                    cancellationToken: cancellationToken);

                // Extract unique document IDs from citations
                HashSet<string> seenDocumentIds = new();
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

                            Dictionary<string, string> tags = new();
                            // Extract tags from citation if available
                            if (citation.Partitions != null && citation.Partitions.Count > 0)
                            {
                                // Tags aren't directly available from citations, so we'll leave them empty
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
                _logger.LogWarning(ex, "Error searching for documents in index: {IndexName}", indexName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error listing documents from index: {IndexName}", indexName);
            // Continue with other indexes even if one fails
        }
    }
}

