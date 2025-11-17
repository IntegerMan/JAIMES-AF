namespace MattEland.Jaimes.Indexer.Services;

public interface IDocumentIndexer
{
    Task<bool> IndexDocumentAsync(string filePath, string indexName, CancellationToken cancellationToken = default);
}

