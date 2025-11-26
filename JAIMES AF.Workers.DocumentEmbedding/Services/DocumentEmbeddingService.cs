using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.Workers.DocumentEmbedding.Configuration;

namespace MattEland.Jaimes.Workers.DocumentEmbedding.Services;

public class DocumentEmbeddingService(
    IMongoClient mongoClient,
    HttpClient httpClient,
    DocumentEmbeddingOptions options,
    QdrantClient qdrantClient,
    ILogger<DocumentEmbeddingService> logger,
    ActivitySource activitySource,
    string ollamaEndpoint,
    string ollamaModel) : IDocumentEmbeddingService
{
    /// <summary>
    /// Generates an embedding using Ollama's HTTP API
    /// </summary>
    private async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken)
    {
        string requestUrl = $"{ollamaEndpoint.TrimEnd('/')}/api/embeddings";
        
        OllamaEmbeddingRequest request = new()
        {
            Model = ollamaModel,
            Prompt = text
        };

        HttpResponseMessage response = await httpClient.PostAsJsonAsync(requestUrl, request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("Failed to generate embedding. Status: {StatusCode}, Response: {Response}",
                response.StatusCode, errorContent);
            response.EnsureSuccessStatusCode();
        }

        OllamaEmbeddingResponse? embeddingResponse = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(
            cancellationToken: cancellationToken);

        if (embeddingResponse?.Embedding == null || embeddingResponse.Embedding.Length == 0)
        {
            throw new InvalidOperationException("Received empty embedding from Ollama");
        }

        return embeddingResponse.Embedding;
    }

    /// <summary>
    /// Generates a Qdrant point ID from a string point ID using SHA256 hash to prevent collisions.
    /// This matches the implementation in QdrantEmbeddingStore.
    /// </summary>
    private static ulong GenerateQdrantPointId(string pointId)
    {
        byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(pointId));
        ulong qdrantPointId = BitConverter.ToUInt64(hashBytes, 0);
        if (qdrantPointId == 0)
        {
            // Qdrant doesn't allow zero IDs
            qdrantPointId = 1;
        }
        return qdrantPointId;
    }

    /// <summary>
    /// Extracts the ruleset ID from the relative directory path.
    /// The ruleset ID is the first directory segment (e.g., "dnd5e" from "dnd5e/sourcebooks/...").
    /// </summary>
    private static string ExtractRulesetId(string? relativeDirectory)
    {
        if (string.IsNullOrWhiteSpace(relativeDirectory))
        {
            return "default";
        }

        string[] parts = relativeDirectory.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        
        return parts.Length > 0 ? parts[0] : "default";
    }

    public async Task ProcessChunkAsync(ChunkReadyForEmbeddingMessage message, CancellationToken cancellationToken = default)
    {
        using Activity? activity = activitySource.StartActivity("DocumentEmbedding.ProcessChunk");
        activity?.SetTag("embedding.chunk_id", message.ChunkId);
        activity?.SetTag("embedding.document_id", message.DocumentId);

        try
        {
            logger.LogDebug("Processing chunk for embedding: {ChunkId} (DocumentId: {DocumentId})",
                message.ChunkId, message.DocumentId);

            // Extract ruleset ID from relative directory
            string rulesetId = ExtractRulesetId(message.RelativeDirectory);
            activity?.SetTag("embedding.ruleset_id", rulesetId);
            logger.LogDebug("Extracted ruleset ID: {RulesetId} from directory: {RelativeDirectory}",
                rulesetId, message.RelativeDirectory);

            // Generate embedding using Ollama HTTP API
            logger.LogDebug("Generating embedding for chunk {ChunkId} (text length: {Length})",
                message.ChunkId, message.ChunkText.Length);
            
            float[] embedding = await GenerateEmbeddingAsync(message.ChunkText, cancellationToken);
            activity?.SetTag("embedding.dimensions", embedding.Length);
            logger.LogDebug("Generated embedding for chunk {ChunkId} with {Dimensions} dimensions",
                message.ChunkId, embedding.Length);

            // Prepare metadata for Qdrant
            Dictionary<string, string> metadata = new()
            {
                { "chunkId", message.ChunkId },
                { "chunkIndex", message.ChunkIndex.ToString() },
                { "chunkText", message.ChunkText },
                { "documentId", message.DocumentId },
                { "fileName", message.FileName },
                { "filePath", message.FilePath },
                { "relativeDirectory", message.RelativeDirectory ?? string.Empty },
                { "rulesetId", rulesetId },
                { "fileSize", message.FileSize.ToString() },
                { "pageCount", message.PageCount.ToString() },
                { "crackedAt", message.CrackedAt.ToString("O") },
                { "embeddedAt", DateTime.UtcNow.ToString("O") }
            };

            // Calculate Qdrant point ID using SHA256 hash
            ulong qdrantPointId = GenerateQdrantPointId(message.ChunkId);
            activity?.SetTag("embedding.qdrant_point_id", qdrantPointId);

            // Ensure collection exists
            await EnsureCollectionExistsAsync(cancellationToken);

            // Store embedding in Qdrant
            await StoreEmbeddingInQdrantAsync(
                message.ChunkId,
                qdrantPointId,
                embedding,
                metadata,
                cancellationToken);

            logger.LogInformation("Stored embedding for chunk {ChunkId} in Qdrant (point ID: {PointId})",
                message.ChunkId, qdrantPointId);

            // Update MongoDB to mark chunk as processed
            await UpdateChunkInMongoDBAsync(message.ChunkId, qdrantPointId, cancellationToken);

            logger.LogInformation("Marked chunk {ChunkId} as processed in MongoDB", message.ChunkId);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process chunk {ChunkId} for embedding", message.ChunkId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    private async Task EnsureCollectionExistsAsync(CancellationToken cancellationToken)
    {
        try
        {
            CollectionInfo? collectionInfo = await qdrantClient.GetCollectionInfoAsync(
                options.CollectionName,
                cancellationToken: cancellationToken);

            if (collectionInfo != null)
            {
                return;
            }
        }
        catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            // Collection doesn't exist, create it
        }

        // Create collection with vector configuration
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

    private async Task StoreEmbeddingInQdrantAsync(
        string pointId,
        ulong qdrantPointId,
        float[] embedding,
        Dictionary<string, string> metadata,
        CancellationToken cancellationToken)
    {
        // Convert metadata to Qdrant payload
        Dictionary<string, Value> payload = new();
        foreach ((string key, string value) in metadata)
        {
            payload[key] = new Value { StringValue = value };
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
    }

    private async Task UpdateChunkInMongoDBAsync(
        string chunkId,
        ulong qdrantPointId,
        CancellationToken cancellationToken)
    {
        IMongoDatabase mongoDatabase = mongoClient.GetDatabase("documents");
        IMongoCollection<DocumentChunk> collection = mongoDatabase.GetCollection<DocumentChunk>("documentChunks");

        FilterDefinition<DocumentChunk> filter = Builders<DocumentChunk>.Filter.Eq(c => c.ChunkId, chunkId);
        UpdateDefinition<DocumentChunk> update = Builders<DocumentChunk>.Update
            .Set(c => c.QdrantPointId, qdrantPointId);

        MongoDB.Driver.UpdateResult result = await collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

        if (result.MatchedCount == 0)
        {
            logger.LogWarning("Chunk {ChunkId} not found in MongoDB when updating Qdrant point ID", chunkId);
        }
        else
        {
            logger.LogDebug("Updated chunk {ChunkId} with Qdrant point ID {PointId}",
                chunkId, qdrantPointId);
        }
    }

    private record OllamaEmbeddingRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; init; }

        [JsonPropertyName("prompt")]
        public required string Prompt { get; init; }
    }

    private record OllamaEmbeddingResponse
    {
        [JsonPropertyName("embedding")]
        public required float[] Embedding { get; init; }
    }
}

