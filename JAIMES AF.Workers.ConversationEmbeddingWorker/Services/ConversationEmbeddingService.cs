using MattEland.Jaimes.ServiceDefaults.Common;

namespace MattEland.Jaimes.Workers.ConversationEmbedding.Services;

public class ConversationEmbeddingService(
    IDbContextFactory<JaimesDbContext> dbContextFactory,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    ConversationEmbeddingOptions options,
    IJaimesEmbeddingClient qdrantClient,
    ILogger<ConversationEmbeddingService> logger,
    ActivitySource activitySource) : IConversationEmbeddingService
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
    /// Processes a conversation message by generating an embedding and storing it in both Qdrant and PostgreSQL.
    /// 
    /// IMPORTANT: Embeddings stored in Qdrant and PostgreSQL will NOT match exactly because:
    /// - Qdrant automatically normalizes vectors when using Cosine distance metric (scales to unit length)
    /// - PostgreSQL (pgvector) stores vectors as-is without normalization
    /// 
    /// This is expected behavior and does not indicate an error. Both stores use the same original embedding,
    /// but Qdrant applies normalization for efficient cosine similarity calculations.
    /// </summary>
    public async Task ProcessConversationMessageAsync(ConversationMessageReadyForEmbeddingMessage message,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = activitySource.StartActivity("ConversationEmbedding.ProcessMessage");
        activity?.SetTag("embedding.message_id", message.MessageId);
        activity?.SetTag("embedding.game_id", message.GameId.ToString());

        try
        {
            logger.LogDebug("Processing conversation message for embedding: MessageId={MessageId}, GameId={GameId}",
                message.MessageId,
                message.GameId);

            // Validate input text to avoid provider400 (e.g., $.input is invalid)
            if (string.IsNullOrWhiteSpace(message.Text))
                throw new ArgumentException("Message text is empty or whitespace and cannot be embedded",
                    nameof(message));

            // Generate embedding using configured embedding generator
            logger.LogDebug("Generating embedding for message {MessageId} (text length: {Length})",
                message.MessageId,
                message.Text.Length);

            // IEmbeddingGenerator.GenerateAsync returns GeneratedEmbeddings, so we get the first result
            GeneratedEmbeddings<Embedding<float>> embeddingsResult =
                await embeddingGenerator.GenerateAsync([message.Text], cancellationToken: cancellationToken);

            if (embeddingsResult.Count == 0)
                throw new InvalidOperationException("Failed to generate embedding for message");

            Embedding<float> embeddingResult = embeddingsResult[0];
            float[] embedding = embeddingResult.Vector.ToArray();

            activity?.SetTag("embedding.dimensions", embedding.Length);
            logger.LogDebug("Generated embedding for message {MessageId} with {Dimensions} dimensions",
                message.MessageId,
                embedding.Length);

            // Prepare metadata for Qdrant
            Dictionary<string, string> metadata = new()
            {
                {"messageId", message.MessageId.ToString()},
                {"gameId", message.GameId.ToString()},
                {"text", message.Text},
                {"role", message.Role.ToString()},
                {"createdAt", message.CreatedAt.ToString("O")},
                {"embeddedAt", DateTime.UtcNow.ToString("O")}
            };

            // Calculate Qdrant point ID using shared utility
            // Use Message.Id.ToString() as the point identifier
            string pointId = message.MessageId.ToString();
            ulong qdrantPointId = GenerateQdrantPointId(pointId);
            activity?.SetTag("embedding.qdrant_point_id", qdrantPointId);

            // Ensure collection exists (dimensions resolved from model)
            await EnsureCollectionExistsAsync(cancellationToken);

            // Store embedding in Qdrant
            // NOTE: Qdrant will automatically normalize this vector when using Cosine distance metric
            await StoreEmbeddingInQdrantAsync(
                pointId,
                qdrantPointId,
                embedding,
                metadata,
                cancellationToken);

            logger.LogInformation("Stored embedding for message {MessageId} in Qdrant (point ID: {PointId})",
                message.MessageId,
                qdrantPointId);

            // Update PostgreSQL to store embedding vector
            // NOTE: PostgreSQL (pgvector) stores the vector as-is without normalization
            // This means the stored values will differ from Qdrant, which is expected behavior
            await UpdateMessageEmbeddingInPostgreSqlAsync(message.MessageId, qdrantPointId, embedding, cancellationToken)
                .ConfigureAwait(false);

            logger.LogInformation("Stored embedding for message {MessageId} in PostgreSQL", message.MessageId);

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process message {MessageId} for embedding", message.MessageId);
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
            foreach ((string key, string value) in metadata) payload[key] = new Value { StringValue = value };

            PointStruct point = new()
            {
                Id = qdrantPointId,
                Vectors = embedding,
                Payload = { payload }
            };

            await qdrantClient.UpsertAsync(
                options.CollectionName,
                [point],
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to store embedding in Qdrant for message {MessageId}", pointId);
            throw;
        }
    }

    /// <summary>
    /// Updates or creates a MessageEmbedding record in PostgreSQL with the Qdrant point ID and embedding vector.
    /// 
    /// NOTE: PostgreSQL (pgvector) stores vectors as-is without normalization.
    /// The stored values will differ from Qdrant (which normalizes for Cosine distance),
    /// but this is expected behavior and does not indicate an error.
    /// </summary>
    private async Task UpdateMessageEmbeddingInPostgreSqlAsync(
        int messageId,
        ulong qdrantPointId,
        float[] embedding,
        CancellationToken cancellationToken)
    {
        try
        {
            await using JaimesDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            MessageEmbedding? messageEmbedding = await dbContext.MessageEmbeddings
                .FirstOrDefaultAsync(me => me.MessageId == messageId, cancellationToken);

            if (messageEmbedding == null)
            {
                // Create new MessageEmbedding record
                messageEmbedding = new MessageEmbedding
                {
                    MessageId = messageId,
                    QdrantPointId = qdrantPointId.ToString(),
                    Embedding = new Pgvector.Vector(embedding),
                    EmbeddedAt = DateTime.UtcNow
                };
                dbContext.MessageEmbeddings.Add(messageEmbedding);
            }
            else
            {
                // Update existing record
                messageEmbedding.QdrantPointId = qdrantPointId.ToString();
                messageEmbedding.Embedding = new Pgvector.Vector(embedding);
                messageEmbedding.EmbeddedAt = DateTime.UtcNow;
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogDebug(
                "Updated message embedding {MessageId} with Qdrant point ID {PointId} and embedding vector ({Dimensions} dimensions)",
                messageId,
                qdrantPointId,
                embedding.Length);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to store embedding in PostgreSQL for message {MessageId}", messageId);
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
            Size = (ulong)dimensions,
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
}

