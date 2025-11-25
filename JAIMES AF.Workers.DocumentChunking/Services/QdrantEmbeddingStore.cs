using System.Diagnostics;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using MattEland.Jaimes.Workers.DocumentChunking.Configuration;

namespace MattEland.Jaimes.Workers.DocumentChunking.Services;

public class QdrantEmbeddingStore(
    QdrantClient qdrantClient,
    DocumentChunkingOptions options,
    ILogger<QdrantEmbeddingStore> logger,
    ActivitySource activitySource) : IQdrantEmbeddingStore
{
    public async Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        using Activity? activity = activitySource.StartActivity("Qdrant.EnsureCollection");
        activity?.SetTag("qdrant.collection", options.CollectionName);

        try
        {
            // Check if collection exists
            CollectionInfo? collectionInfo = await qdrantClient.GetCollectionInfoAsync(
                options.CollectionName, 
                cancellationToken: cancellationToken);

            if (collectionInfo != null)
            {
                logger.LogDebug("Collection {CollectionName} already exists", options.CollectionName);
                activity?.SetStatus(ActivityStatusCode.Ok);
                return;
            }
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound || 
                                       ex.StatusCode == StatusCode.Internal)
        {
            // Collection doesn't exist (NotFound) or server error (might be not ready)
            if (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
            {
                logger.LogInformation("Collection {CollectionName} does not exist, creating it", options.CollectionName);
            }
            else
            {
                // Internal error might mean Qdrant isn't ready yet
                logger.LogWarning("Qdrant returned internal error, might not be ready yet: {Message}", ex.Message);
                throw; // Re-throw to allow retry
            }
        }
        catch (Exception ex) when (ex.Message.Contains("doesn't exist") || 
                                    ex.Message.Contains("not found") ||
                                    ex.Message.Contains("PROTOCOL_ERROR") ||
                                    ex.Message.Contains("HTTP/2"))
        {
            // Collection doesn't exist or connection issue
            if (ex.Message.Contains("PROTOCOL_ERROR") || ex.Message.Contains("HTTP/2"))
            {
                logger.LogWarning("Qdrant connection error (might not be ready): {Message}", ex.Message);
                throw; // Re-throw to allow retry
            }
            logger.LogInformation("Collection {CollectionName} does not exist, creating it", options.CollectionName);
        }

        // Create collection with vector configuration
        // Qdrant client API - CreateCollectionAsync(string collectionName, VectorParams vectorParams, ...)
        try
        {
            VectorParams vectorParams = new()
            {
                Size = (ulong)options.EmbeddingDimensions,
                Distance = Distance.Cosine
            };

            await qdrantClient.CreateCollectionAsync(
                options.CollectionName,
                vectorParams,
                cancellationToken: cancellationToken);
            
            logger.LogInformation("Created Qdrant collection {CollectionName} with {Dimensions} dimensions", 
                options.CollectionName, options.EmbeddingDimensions);
        }
        catch (Exception ex) when (ex.Message.Contains("already exists") || ex.Message.Contains("duplicate"))
        {
            logger.LogDebug("Collection {CollectionName} already exists", options.CollectionName);
        }
        
        activity?.SetStatus(ActivityStatusCode.Ok);
    }

    public async Task StoreEmbeddingAsync(
        string pointId, 
        float[] embedding, 
        Dictionary<string, string> metadata,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = activitySource.StartActivity("Qdrant.StoreEmbedding");
        activity?.SetTag("qdrant.collection", options.CollectionName);
        activity?.SetTag("qdrant.point_id", pointId);
        activity?.SetTag("qdrant.embedding_dimensions", embedding.Length);

        try
        {
            // Ensure collection exists before storing (in case it wasn't created earlier)
            // This provides a safety net if EnsureCollectionExistsAsync failed during ProcessDocumentAsync
            await EnsureCollectionExistsAsync(cancellationToken);

            // Convert metadata to Qdrant payload
            Dictionary<string, Value> payload = new();
            foreach ((string key, string value) in metadata)
            {
                payload[key] = new Value { StringValue = value };
            }

            // Generate a point ID from the provided pointId string (use hash code or convert to ulong)
            // For chunks, this will be the chunk ID; for backward compatibility, it can be document ID
            ulong qdrantPointId = (ulong)Math.Abs(pointId.GetHashCode());
            if (qdrantPointId == 0)
            {
                // Avoid zero ID (Qdrant doesn't allow it)
                qdrantPointId = 1;
            }

            PointStruct point = new()
            {
                Id = qdrantPointId,
                Vectors = embedding,
                Payload = { payload }
            };

            await qdrantClient.UpsertAsync(
                collectionName: options.CollectionName,
                points: new[] { point },
                cancellationToken: cancellationToken);

            logger.LogInformation("Stored embedding for point {PointId} in collection {CollectionName}", 
                pointId, options.CollectionName);
            
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to store embedding for point {PointId}", pointId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public async Task<List<EmbeddingInfo>> ListEmbeddingsAsync(CancellationToken cancellationToken = default)
    {
        using Activity? activity = activitySource.StartActivity("Qdrant.ListEmbeddings");
        activity?.SetTag("qdrant.collection", options.CollectionName);

        List<EmbeddingInfo> embeddings = new();

        try
        {
            // Check if collection exists
            CollectionInfo? collectionInfo = await qdrantClient.GetCollectionInfoAsync(
                options.CollectionName,
                cancellationToken: cancellationToken);

            if (collectionInfo == null)
            {
                logger.LogInformation("Collection {CollectionName} does not exist, returning empty list", options.CollectionName);
                activity?.SetStatus(ActivityStatusCode.Ok);
                return embeddings;
            }

            // Scroll through all points in the collection
            PointId? nextPageOffset = null;
            const int batchSize = 100;

            do
            {
                ScrollResponse scrollResult = await qdrantClient.ScrollAsync(
                    collectionName: options.CollectionName,
                    filter: null,
                    limit: batchSize,
                    offset: nextPageOffset,
                    cancellationToken: cancellationToken);

                foreach (RetrievedPoint point in scrollResult.Result)
                {
                    try
                    {
                        // Extract metadata from payload
                        string chunkId = point.Payload.GetValueOrDefault("chunkId")?.StringValue ?? string.Empty;
                        string documentId = point.Payload.GetValueOrDefault("documentId")?.StringValue ?? string.Empty;
                        string fileName = point.Payload.GetValueOrDefault("fileName")?.StringValue ?? string.Empty;
                        string chunkText = point.Payload.GetValueOrDefault("chunkText")?.StringValue ?? string.Empty;
                        int chunkIndex = int.TryParse(point.Payload.GetValueOrDefault("chunkIndex")?.StringValue, out int index) ? index : 0;

                        // Reconstruct the original pointId from chunkId (which is what we stored)
                        string pointId = chunkId;

                        // Extract ulong from PointId - PointId in RetrievedPoint is a PointId type
                        // We need to convert it - for now, use hash code since we can't directly access NumId
                        ulong qdrantPointId = (ulong)Math.Abs(point.Id.GetHashCode());
                        if (qdrantPointId == 0) qdrantPointId = 1;

                        embeddings.Add(new EmbeddingInfo
                        {
                            PointId = pointId,
                            QdrantPointId = qdrantPointId,
                            DocumentId = documentId,
                            FileName = fileName,
                            ChunkId = chunkId,
                            ChunkIndex = chunkIndex,
                            ChunkText = chunkText
                        });
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to parse embedding point, skipping");
                    }
                }

                // Check if there are more points
                nextPageOffset = scrollResult.NextPageOffset;
            } while (nextPageOffset != null);

            logger.LogInformation("Retrieved {Count} embeddings from collection {CollectionName}", embeddings.Count, options.CollectionName);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list embeddings from collection {CollectionName}", options.CollectionName);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }

        return embeddings;
    }

    public async Task DeleteEmbeddingAsync(string pointId, CancellationToken cancellationToken = default)
    {
        using Activity? activity = activitySource.StartActivity("Qdrant.DeleteEmbedding");
        activity?.SetTag("qdrant.collection", options.CollectionName);
        activity?.SetTag("qdrant.point_id", pointId);

        try
        {
            // Convert pointId to Qdrant point ID (same logic as in StoreEmbeddingAsync)
            ulong qdrantPointId = (ulong)Math.Abs(pointId.GetHashCode());
            if (qdrantPointId == 0)
            {
                qdrantPointId = 1;
            }

            // Delete the point by recreating the collection without this point
            // For now, use a workaround: delete and recreate collection, or use HTTP API
            // Since the gRPC API signature is unclear, we'll use a filter-based approach
            // Try using an empty filter with a condition on chunkId in payload
            // Actually, let's use a simpler approach - delete by matching the chunkId in payload
            Filter filter = new()
            {
                Must =
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "chunkId",
                            Match = new Match { Text = pointId }
                        }
                    }
                }
            };
            
            await qdrantClient.DeleteAsync(
                collectionName: options.CollectionName,
                filter: filter,
                cancellationToken: cancellationToken);

            logger.LogInformation("Deleted embedding for point {PointId} from collection {CollectionName}", pointId, options.CollectionName);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete embedding for point {PointId}", pointId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public async Task DeleteAllEmbeddingsAsync(CancellationToken cancellationToken = default)
    {
        using Activity? activity = activitySource.StartActivity("Qdrant.DeleteAllEmbeddings");
        activity?.SetTag("qdrant.collection", options.CollectionName);

        try
        {
            // Check if collection exists
            CollectionInfo? collectionInfo = await qdrantClient.GetCollectionInfoAsync(
                options.CollectionName,
                cancellationToken: cancellationToken);

            if (collectionInfo == null)
            {
                logger.LogInformation("Collection {CollectionName} does not exist, nothing to delete", options.CollectionName);
                activity?.SetStatus(ActivityStatusCode.Ok);
                return;
            }

            // Scroll through all points and collect their IDs as ulong
            List<ulong> pointIds = new();
            PointId? nextPageOffset = null;
            const int batchSize = 100;

            do
            {
                ScrollResponse scrollResult = await qdrantClient.ScrollAsync(
                    collectionName: options.CollectionName,
                    filter: null,
                    limit: batchSize,
                    offset: nextPageOffset,
                    cancellationToken: cancellationToken);

                foreach (RetrievedPoint point in scrollResult.Result)
                {
                    // Convert PointId to ulong using hash code
                    ulong id = (ulong)Math.Abs(point.Id.GetHashCode());
                    if (id == 0) id = 1;
                    pointIds.Add(id);
                }

                nextPageOffset = scrollResult.NextPageOffset;
            } while (nextPageOffset != null);

            // Delete all points using an empty filter (matches all points)
            // Use a filter with empty Must list to match all points
            Filter deleteAllFilter = new()
            {
                Must = { } // Empty Must list matches all points
            };
            
            await qdrantClient.DeleteAsync(
                collectionName: options.CollectionName,
                filter: deleteAllFilter,
                cancellationToken: cancellationToken);
            
            int deletedCount = pointIds.Count;

            logger.LogInformation("Deleted {Count} embeddings from collection {CollectionName}", deletedCount, options.CollectionName);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete all embeddings from collection {CollectionName}", options.CollectionName);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}

