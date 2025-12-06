// added for utilities

namespace MattEland.Jaimes.Workers.DocumentEmbedding.Services;

public class DocumentEmbeddingService(
    IDbContextFactory<JaimesDbContext> dbContextFactory,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    DocumentEmbeddingOptions options,
    IJaimesEmbeddingClient qdrantClient,
    ILogger<DocumentEmbeddingService> logger,
    ActivitySource activitySource) : IDocumentEmbeddingService
{
    // Lazily determined embedding dimensions from the embedding model
    private int? _resolvedEmbeddingDimensions;

    /// <summary>
    /// Ensures the embedding dimensions are resolved based on the configured embedding model.
    /// </summary>
    private async Task<int> ResolveEmbeddingDimensionsAsync(CancellationToken cancellationToken)
    {
        if (_resolvedEmbeddingDimensions.HasValue) return _resolvedEmbeddingDimensions.Value;

        int dims = await QdrantUtilities.ResolveEmbeddingDimensionsAsync(embeddingGenerator, logger, cancellationToken);
        if (dims > 0) _resolvedEmbeddingDimensions = dims;
        return dims;
    }

    /// <summary>
    /// Generates a Qdrant point ID from a string point ID using SHA256 hash to prevent collisions.
    /// Utility wrapper to shared implementation.
    /// </summary>
    private static ulong GenerateQdrantPointId(string pointId)
    {
        return QdrantUtilities.GeneratePointId(pointId);
    }

    /// <summary>
    /// Extracts the ruleset ID from the relative directory path.
    /// The ruleset ID is the first directory segment (e.g., "dnd5e" from "dnd5e/sourcebooks/...").
    /// </summary>
    private static string ExtractRulesetId(string? relativeDirectory)
    {
        if (string.IsNullOrWhiteSpace(relativeDirectory)) return "default";

        string[] parts = relativeDirectory.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        return parts.Length > 0 ? parts[0] : "default";
    }

    /// <summary>
    /// Processes a chunk by generating an embedding and storing it in both Qdrant and PostgreSQL.
    /// 
    /// IMPORTANT: Embeddings stored in Qdrant and PostgreSQL will NOT match exactly because:
    /// - Qdrant automatically normalizes vectors when using Cosine distance metric (scales to unit length)
    /// - PostgreSQL (pgvector) stores vectors as-is without normalization
    /// 
    /// This is expected behavior and does not indicate an error. Both stores use the same original embedding,
    /// but Qdrant applies normalization for efficient cosine similarity calculations.
    /// </summary>
    public async Task ProcessChunkAsync(ChunkReadyForEmbeddingMessage message,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = activitySource.StartActivity("DocumentEmbedding.ProcessChunk");
        activity?.SetTag("embedding.chunk_id", message.ChunkId);
        activity?.SetTag("embedding.document_id", message.DocumentId);

        try
        {
            logger.LogDebug("Processing chunk for embedding: {ChunkId} (DocumentId: {DocumentId})",
                message.ChunkId,
                message.DocumentId);

            // Validate input text to avoid provider400 (e.g., $.input is invalid)
            if (string.IsNullOrWhiteSpace(message.ChunkText))
                throw new ArgumentException("Chunk text is empty or whitespace and cannot be embedded",
                    nameof(message));

            // Use ruleset ID from message (or extract from relative directory as fallback)
            string rulesetId = !string.IsNullOrWhiteSpace(message.RulesetId)
                ? message.RulesetId
                : ExtractRulesetId(message.RelativeDirectory);
            activity?.SetTag("embedding.ruleset_id", rulesetId);
            logger.LogDebug("Using ruleset ID: {RulesetId} for chunk {ChunkId}",
                rulesetId,
                message.ChunkId);

            // Generate embedding using configured embedding generator
            logger.LogDebug("Generating embedding for chunk {ChunkId} (text length: {Length})",
                message.ChunkId,
                message.ChunkText.Length);

            // IEmbeddingGenerator.GenerateAsync returns GeneratedEmbeddings, so we get the first result
            GeneratedEmbeddings<Embedding<float>> embeddingsResult =
                await embeddingGenerator.GenerateAsync([message.ChunkText], cancellationToken: cancellationToken);

            if (embeddingsResult.Count == 0)
                throw new InvalidOperationException("Failed to generate embedding for chunk");

            Embedding<float> embeddingResult = embeddingsResult[0];
            float[] embedding = embeddingResult.Vector.ToArray();

            activity?.SetTag("embedding.dimensions", embedding.Length);
            logger.LogDebug("Generated embedding for chunk {ChunkId} with {Dimensions} dimensions",
                message.ChunkId,
                embedding.Length);

            // Prepare metadata for Qdrant
            Dictionary<string, string> metadata = new()
            {
                {"chunkId", message.ChunkId},
                {"chunkIndex", message.ChunkIndex.ToString()},
                {"chunkText", message.ChunkText},
                {"documentId", message.DocumentId},
                {"fileName", message.FileName},
                {"rulesetId", rulesetId},
                {"fileSize", message.FileSize.ToString()},
                {"documentKind", message.DocumentKind},
                {"embeddedAt", DateTime.UtcNow.ToString("O")}
            };

            // Add page number if available
            if (message.PageNumber.HasValue) metadata["pageNumber"] = message.PageNumber.Value.ToString();

            // Calculate Qdrant point ID using shared utility
            ulong qdrantPointId = GenerateQdrantPointId(message.ChunkId);
            activity?.SetTag("embedding.qdrant_point_id", qdrantPointId);

            // Ensure collection exists (dimensions resolved from model)
            await EnsureCollectionExistsAsync(cancellationToken);

            // Store embedding in Qdrant
            // NOTE: Qdrant will automatically normalize this vector when using Cosine distance metric
            await StoreEmbeddingInQdrantAsync(
                message.ChunkId,
                qdrantPointId,
                embedding,
                metadata,
                cancellationToken);

            logger.LogInformation("Stored embedding for chunk {ChunkId} in Qdrant (point ID: {PointId})",
                message.ChunkId,
                qdrantPointId);

            // Update PostgreSQL to mark chunk as processed and store embedding vector
            // NOTE: PostgreSQL (pgvector) stores the vector as-is without normalization
            // This means the stored values will differ from Qdrant, which is expected behavior
            await UpdateChunkInPostgreSqlAsync(message.ChunkId, qdrantPointId, embedding, cancellationToken)
                .ConfigureAwait(false);

            // Increment ProcessedChunkCount in CrackedDocument
            if (!int.TryParse(message.DocumentId, out int documentId))
            {
                logger.LogError(
                    "Failed to parse DocumentId '{DocumentId}' as integer for chunk {ChunkId}. ProcessedChunkCount will not be incremented.",
                    message.DocumentId,
                    message.ChunkId);
                throw new ArgumentException($"Invalid DocumentId format: '{message.DocumentId}'. Expected an integer.",
                    nameof(message));
            }

            await IncrementProcessedChunkCountAsync(documentId, cancellationToken);

            logger.LogInformation("Marked chunk {ChunkId} as processed in PostgreSQL", message.ChunkId);

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process chunk {ChunkId} for embedding", message.ChunkId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Stores an embedding in Qdrant.
    /// 
    /// NOTE: When using Cosine distance metric, Qdrant automatically normalizes vectors upon insertion
    /// (scales them to unit length). The stored vector will differ from the original embedding.
    /// </summary>
    private async Task StoreEmbeddingInQdrantAsync(
        string pointId,
        ulong qdrantPointId,
        float[] embedding,
        Dictionary<string, string> metadata,
        CancellationToken cancellationToken)
    {
        try
        {
            // Convert metadata to Qdrant payload
            Dictionary<string, Value> payload = new();
            foreach ((string key, string value) in metadata) payload[key] = new Value {StringValue = value};

            PointStruct point = new()
            {
                Id = qdrantPointId,
                Vectors = embedding,
                Payload = {payload}
            };

            await qdrantClient.UpsertAsync(
                options.CollectionName,
                [point],
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to store embedding in Qdrant for chunk {ChunkId}", pointId);
            throw;
        }
    }

    /// <summary>
    /// Updates a chunk in PostgreSQL with the Qdrant point ID and embedding vector.
    /// 
    /// NOTE: PostgreSQL (pgvector) stores vectors as-is without normalization.
    /// The stored values will differ from Qdrant (which normalizes for Cosine distance),
    /// but this is expected behavior and does not indicate an error.
    /// </summary>
    private async Task UpdateChunkInPostgreSqlAsync(
        string chunkId,
        ulong qdrantPointId,
        float[] embedding,
        CancellationToken cancellationToken)
    {
        try
        {
            await using JaimesDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            DocumentChunk? chunk = await dbContext.DocumentChunks
                .FirstOrDefaultAsync(c => c.ChunkId == chunkId, cancellationToken);

            if (chunk == null)
            {
                logger.LogWarning("Chunk {ChunkId} not found in PostgreSQL when updating Qdrant point ID and embedding",
                    chunkId);
            }
            else
            {
                chunk.QdrantPointId = qdrantPointId.ToString();
                chunk.Embedding = new Pgvector.Vector(embedding);
                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogDebug(
                    "Updated chunk {ChunkId} with Qdrant point ID {PointId} and embedding vector ({Dimensions} dimensions)",
                    chunkId,
                    qdrantPointId,
                    embedding.Length);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to store embedding in PostgreSQL for chunk {ChunkId}", chunkId);
            throw;
        }
    }

    private async Task EnsureCollectionExistsAsync(CancellationToken cancellationToken)
    {
        try
        {
            CollectionInfo? collectionInfo = await qdrantClient.GetCollectionInfoAsync(
                options.CollectionName,
                cancellationToken);

            if (collectionInfo != null) return;
        }
        catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            // Collection doesn't exist, create it
        }

        // Resolve vector size from the embedding model
        int dimensions = await ResolveEmbeddingDimensionsAsync(cancellationToken);

        // Create collection with vector configuration
        VectorParams vectorParams = new()
        {
            Size = (ulong) dimensions,
            Distance = Distance.Cosine
        };

        await qdrantClient.CreateCollectionAsync(
            options.CollectionName,
            vectorParams,
            cancellationToken);

        logger.LogInformation("Created Qdrant collection {CollectionName} with {Dimensions} dimensions",
            options.CollectionName,
            dimensions);
    }


    private async Task IncrementProcessedChunkCountAsync(
        int documentId,
        CancellationToken cancellationToken)
    {
        using Activity? activity = activitySource.StartActivity("DocumentEmbedding.IncrementProcessedChunkCount");
        activity?.SetTag("embedding.document_id", documentId);

        try
        {
            await using JaimesDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            // Check if the database provider supports ExecuteUpdateAsync
            // In-memory database doesn't support it, so we use a fallback for tests
            bool supportsExecuteUpdate = dbContext.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory";

            if (supportsExecuteUpdate)
            {
                // Use atomic database update to prevent race conditions when multiple chunks
                // for the same document are processed concurrently (PostgreSQL, SQL Server, etc.)
                int rowsAffected = await dbContext.CrackedDocuments
                    .Where(d => d.Id == documentId)
                    .ExecuteUpdateAsync(
                        setter => setter.SetProperty(d => d.ProcessedChunkCount, d => d.ProcessedChunkCount + 1),
                        cancellationToken);

                if (rowsAffected == 0)
                    logger.LogWarning("Document {DocumentId} not found when incrementing processed chunk count",
                        documentId);
                else
                    logger.LogDebug("Incremented processed chunk count for document {DocumentId}", documentId);
            }
            else
            {
                // Fallback for database providers that don't support ExecuteUpdateAsync (e.g., in-memory database in tests)
                // Note: This fallback has a race condition risk, but is acceptable for test scenarios
                CrackedDocument? document = await dbContext.CrackedDocuments
                    .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);

                if (document == null)
                {
                    logger.LogWarning("Document {DocumentId} not found when incrementing processed chunk count",
                        documentId);
                }
                else
                {
                    document.ProcessedChunkCount++;
                    await dbContext.SaveChangesAsync(cancellationToken);
                    logger.LogDebug(
                        "Incremented processed chunk count for document {DocumentId} (using fallback method)",
                        documentId);
                }
            }

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to increment processed chunk count for document {DocumentId}", documentId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            // Don't throw - this is a non-critical operation
        }
    }
}