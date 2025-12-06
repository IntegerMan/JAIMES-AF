namespace MattEland.Jaimes.Workers.DocumentEmbedding.Services;

/// <summary>
/// Wrapper around QdrantClient to enable testability and mocking.
/// </summary>
public class QdrantClientWrapper(QdrantClient client) : IJaimesEmbeddingClient
{
    private readonly QdrantClient _client = client ?? throw new ArgumentNullException(nameof(client));

    public async Task<CollectionInfo?> GetCollectionInfoAsync(string collectionName,
        CancellationToken cancellationToken = default)
    {
        CollectionInfo? result = await _client.GetCollectionInfoAsync(collectionName, cancellationToken);
        return result;
    }

    public Task CreateCollectionAsync(
        string collectionName,
        VectorParams vectorParams,
        CancellationToken cancellationToken = default)
    {
        return _client.CreateCollectionAsync(
            collectionName,
            vectorParams,
            cancellationToken: cancellationToken);
    }

    public Task<UpdateResult> UpsertAsync(
        string collectionName,
        PointStruct[] points,
        CancellationToken cancellationToken = default)
    {
        return _client.UpsertAsync(
            collectionName,
            points,
            cancellationToken: cancellationToken);
    }
}