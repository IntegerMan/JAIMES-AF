using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;

namespace MattEland.Jaimes.Indexer.Services;

public class DocumentIndexer : IDocumentIndexer
{
    private readonly ILogger<DocumentIndexer> _logger;
    private readonly IKernelMemory _memory;

    public DocumentIndexer(ILogger<DocumentIndexer> logger, IKernelMemory memory)
    {
        _logger = logger;
        _memory = memory;
    }

    public async Task<bool> DocumentExistsAsync(string filePath, string indexName, CancellationToken cancellationToken = default)
    {
        try
        {
            string documentId = GetDocumentId(filePath);
            DataPipelineStatus? status = await _memory.GetDocumentStatusAsync(documentId, indexName, cancellationToken);
            return status != null && status.Completed;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking if document exists: {FilePath}", filePath);
            return false;
        }
    }

    public async Task<bool> IndexDocumentAsync(string filePath, string indexName, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("File does not exist: {FilePath}", filePath);
            return false;
        }

        try
        {
            string documentId = GetDocumentId(filePath);
            string fileName = Path.GetFileName(filePath);
            
            _logger.LogInformation("Indexing document: {FilePath} with ID: {DocumentId} in index: {IndexName}", filePath, documentId, indexName);
            
            await _memory.ImportDocumentAsync(
                new Document(documentId)
                    .AddFile(filePath)
                    .AddTag("sourcePath", filePath)
                    .AddTag("fileName", fileName),
                index: indexName,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Successfully indexed document: {FilePath}", filePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error indexing document: {FilePath}", filePath);
            return false;
        }
    }

    private static string GetDocumentId(string filePath)
    {
        // Use a normalized path as document ID
        return $"doc-{filePath.Replace("\\", "/").Replace(":", "-").ToLowerInvariant()}";
    }
}

