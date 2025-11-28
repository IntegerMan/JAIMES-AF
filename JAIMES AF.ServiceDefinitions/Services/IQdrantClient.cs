using Qdrant.Client.Grpc;

namespace MattEland.Jaimes.ServiceDefinitions.Services;

/// <summary>
/// Interface for Qdrant client operations, allowing for testability and mocking.
/// </summary>
public interface IQdrantClient
{
    /// <summary>
    /// Gets information about a collection.
    /// </summary>
    Task<CollectionInfo?> GetCollectionInfoAsync(string collectionName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new collection with the specified vector parameters.
    /// </summary>
    Task CreateCollectionAsync(
        string collectionName,
        VectorParams vectorParams,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts points into a collection.
    /// </summary>
    Task<UpdateResult> UpsertAsync(
        string collectionName,
        PointStruct[] points,
        CancellationToken cancellationToken = default);
}

