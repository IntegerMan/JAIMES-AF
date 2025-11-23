using System.Diagnostics;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using MattEland.Jaimes.Workers.DocumentEmbeddings.Configuration;

namespace MattEland.Jaimes.Workers.DocumentEmbeddings.Services;

public class QdrantEmbeddingStore(
    QdrantClient qdrantClient,
    EmbeddingWorkerOptions options,
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
        string documentId, 
        float[] embedding, 
        Dictionary<string, string> metadata,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = activitySource.StartActivity("Qdrant.StoreEmbedding");
        activity?.SetTag("qdrant.collection", options.CollectionName);
        activity?.SetTag("qdrant.document_id", documentId);
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

            // Generate a point ID from document ID (use hash code or convert to ulong)
            // For simplicity, we'll use a hash of the document ID
            ulong pointId = (ulong)Math.Abs(documentId.GetHashCode());
            if (pointId == 0)
            {
                // Avoid zero ID (Qdrant doesn't allow it)
                pointId = 1;
            }

            PointStruct point = new()
            {
                Id = pointId,
                Vectors = embedding,
                Payload = { payload }
            };

            await qdrantClient.UpsertAsync(
                collectionName: options.CollectionName,
                points: new[] { point },
                cancellationToken: cancellationToken);

            logger.LogInformation("Stored embedding for document {DocumentId} in collection {CollectionName}", 
                documentId, options.CollectionName);
            
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to store embedding for document {DocumentId}", documentId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}

