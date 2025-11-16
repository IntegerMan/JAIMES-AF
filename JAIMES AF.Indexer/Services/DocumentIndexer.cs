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

    public async Task<string?> GetDocumentHashAsync(string filePath, string indexName, CancellationToken cancellationToken = default)
    {
        try
        {
            string documentId = GetDocumentId(filePath);
            DataPipelineStatus? status = await _memory.GetDocumentStatusAsync(documentId, indexName, cancellationToken);
            
            if (status == null || !status.Completed)
            {
                return null;
            }

            // Document exists in Kernel Memory
            // Note: Kernel Memory's SearchAsync doesn't easily expose tags from Citation objects
            // We store the hash as a tag when indexing, but retrieving it requires querying the underlying storage
            // For now, we'll return null to indicate we can't retrieve the stored hash
            // The orchestrator will handle this by computing the current hash and re-indexing
            // Kernel Memory will update the document with the new hash tag
            // This means we'll re-index existing documents, but Kernel Memory handles updates efficiently
            return null; // Can't easily retrieve hash from Kernel Memory - will need to re-index to update
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking document status: {FilePath}", filePath);
            return null;
        }
    }

    public async Task<bool> IndexDocumentAsync(string filePath, string indexName, string fileHash, CancellationToken cancellationToken = default)
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
                    .AddTag("fileName", fileName)
                    .AddTag("fileHash", fileHash),
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

