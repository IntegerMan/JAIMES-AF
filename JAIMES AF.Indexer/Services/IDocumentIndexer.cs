namespace MattEland.Jaimes.Indexer.Services;

public interface IDocumentIndexer
{
    Task<bool> IndexDocumentAsync(string filePath, string indexName, string fileHash, CancellationToken cancellationToken = default);
    Task<bool> DocumentExistsAsync(string filePath, string indexName, CancellationToken cancellationToken = default);
}

