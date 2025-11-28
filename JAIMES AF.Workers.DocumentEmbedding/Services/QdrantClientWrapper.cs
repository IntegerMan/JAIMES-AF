using Qdrant.Client;
using Qdrant.Client.Grpc;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.Workers.DocumentEmbedding.Services;

/// <summary>
/// Wrapper around QdrantClient to enable testability and mocking.
/// </summary>
public class QdrantClientWrapper : IQdrantClient
{
    private readonly QdrantClient _client;

    public QdrantClientWrapper(QdrantClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<CollectionInfo?> GetCollectionInfoAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        CollectionInfo? result = await _client.GetCollectionInfoAsync(collectionName, cancellationToken: cancellationToken);
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

