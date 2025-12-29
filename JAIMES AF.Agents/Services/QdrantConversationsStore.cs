using MattEland.Jaimes.ServiceDefaults.Common;

namespace MattEland.Jaimes.Agents.Services;

public class QdrantConversationsStore(
    QdrantClient qdrantClient,
    ILogger<QdrantConversationsStore> logger,
    ActivitySource activitySource) : IQdrantConversationsStore
{
    private const string CollectionName = "conversations";

    // Standard embedding dimensions for text-embedding-3-small model
    // This must match between indexing and searching for vectors to be compatible
    private const int EmbeddingDimensions = 1536;

    public async Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        using Activity? activity = activitySource.StartActivity("QdrantConversations.EnsureCollection");
        activity?.SetTag("qdrant.collection", CollectionName);

        try
        {
            // Check if collection exists
            CollectionInfo? collectionInfo = await qdrantClient.GetCollectionInfoAsync(
                CollectionName,
                cancellationToken);

            if (collectionInfo != null)
            {
                logger.LogDebug("Collection {CollectionName} already exists", CollectionName);
                activity?.SetStatus(ActivityStatusCode.Ok);
                return;
            }
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound ||
                                      ex.StatusCode == StatusCode.Internal)
        {
            // Collection doesn't exist (NotFound) or server error (might be not ready)
            if (ex.StatusCode == StatusCode.NotFound)
            {
                logger.LogInformation("Collection {CollectionName} does not exist, creating it", CollectionName);
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

            logger.LogInformation("Collection {CollectionName} does not exist, creating it", CollectionName);
        }

        // Create collection with vector configuration
        try
        {
            VectorParams vectorParams = new()
            {
                Size = (ulong)EmbeddingDimensions,
                Distance = Distance.Cosine
            };

            await qdrantClient.CreateCollectionAsync(
                CollectionName,
                vectorParams,
                cancellationToken: cancellationToken);

            logger.LogInformation("Created Qdrant collection {CollectionName} with {Dimensions} dimensions",
                CollectionName,
                EmbeddingDimensions);
        }
        catch (Exception ex) when (ex.Message.Contains("already exists") || ex.Message.Contains("duplicate"))
        {
            logger.LogDebug("Collection {CollectionName} already exists", CollectionName);
        }

        activity?.SetStatus(ActivityStatusCode.Ok);
    }

    public async Task StoreConversationAsync(
        string messageId,
        float[] embedding,
        Dictionary<string, string> metadata,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = activitySource.StartActivity("QdrantConversations.StoreConversation");
        activity?.SetTag("qdrant.collection", CollectionName);
        activity?.SetTag("qdrant.message_id", messageId);
        activity?.SetTag("qdrant.embedding_dimensions", embedding.Length);

        try
        {
            // Ensure collection exists before storing
            await EnsureCollectionExistsAsync(cancellationToken);

            // Convert metadata to Qdrant payload
            Dictionary<string, Value> payload = new();
            foreach ((string key, string value) in metadata) payload[key] = new Value { StringValue = value };

            // Generate a point ID from the message ID string using SHA256 hash
            ulong qdrantPointId = QdrantUtilities.GeneratePointId(messageId);

            PointStruct point = new()
            {
                Id = qdrantPointId,
                Vectors = embedding,
                Payload = { payload }
            };

            await qdrantClient.UpsertAsync(
                CollectionName,
                new[] { point },
                cancellationToken: cancellationToken);

            logger.LogInformation("Stored conversation message {MessageId} in collection {CollectionName}",
                messageId,
                CollectionName);

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to store conversation message {MessageId}", messageId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public async Task<List<ConversationSearchHit>> SearchConversationsAsync(
        float[] queryEmbedding,
        Guid gameId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = activitySource.StartActivity("QdrantConversations.Search");
        activity?.SetTag("qdrant.collection", CollectionName);
        activity?.SetTag("qdrant.limit", limit);
        activity?.SetTag("qdrant.game_id", gameId.ToString());

        try
        {
            if (queryEmbedding.Length != EmbeddingDimensions)
                throw new ArgumentException(
                    $"Query embedding has {queryEmbedding.Length} dimensions, expected {EmbeddingDimensions}");

            // Build filter to search only within the specified game
            Filter filter = new Filter
            {
                Must =
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "gameId",
                            Match = new Match { Text = gameId.ToString() }
                        }
                    }
                }
            };

            // Search for similar vectors using Qdrant client
            IReadOnlyList<ScoredPoint> searchResults = await qdrantClient.SearchAsync(
                CollectionName,
                queryEmbedding,
                filter,
                limit: (ulong)limit,
                cancellationToken: cancellationToken);

            List<ConversationSearchHit> results = new();
            foreach (ScoredPoint point in searchResults)
                try
                {
                    string messageIdStr = point.Payload.GetValueOrDefault("messageId")?.StringValue ?? string.Empty;
                    string text = point.Payload.GetValueOrDefault("text")?.StringValue ?? string.Empty;
                    string gameIdStr = point.Payload.GetValueOrDefault("gameId")?.StringValue ?? string.Empty;
                    string role = point.Payload.GetValueOrDefault("role")?.StringValue ?? string.Empty;
                    string createdAtStr = point.Payload.GetValueOrDefault("createdAt")?.StringValue ?? string.Empty;

                    if (!string.IsNullOrWhiteSpace(messageIdStr) && 
                        !string.IsNullOrWhiteSpace(text) &&
                        int.TryParse(messageIdStr, out int messageId) &&
                        Guid.TryParse(gameIdStr, out Guid parsedGameId) &&
                        DateTime.TryParse(createdAtStr, out DateTime createdAt))
                    {
                        results.Add(new ConversationSearchHit
                        {
                            MessageId = messageId,
                            Text = text,
                            GameId = parsedGameId,
                            Role = role,
                            CreatedAt = createdAt,
                            Score = point.Score
                        });
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to parse conversation search result, skipping");
                }

            logger.LogInformation("Found {Count} conversation messages matching query for game {GameId}", results.Count, gameId);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return results;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to search conversations");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}

