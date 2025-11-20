namespace MattEland.Jaimes.Indexer.Services;

public interface IDocumentIndexer
{
    Task<string> IndexDocumentAsync(string filePath, string indexName, CancellationToken cancellationToken = default);
}

