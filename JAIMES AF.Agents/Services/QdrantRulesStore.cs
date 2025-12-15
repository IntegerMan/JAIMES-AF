using MattEland.Jaimes.ServiceDefaults.Common;

namespace MattEland.Jaimes.Agents.Services;

public class QdrantRulesStore(
    QdrantClient qdrantClient,
    ILogger<QdrantRulesStore> logger,
    ActivitySource activitySource) : IQdrantRulesStore
{
    private const string CollectionName = "rulesets";

    private const string DocumentEmbeddingsCollectionName = "document-embeddings";

    // Standard embedding dimensions for text-embedding-3-small model
    // This must match between indexing and searching for vectors to be compatible
    private const int EmbeddingDimensions = 1536;

    public async Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        using Activity? activity = activitySource.StartActivity("QdrantRules.EnsureCollection");
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

    public async Task StoreRuleAsync(
        string ruleId,
        float[] embedding,
        Dictionary<string, string> metadata,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = activitySource.StartActivity("QdrantRules.StoreRule");
        activity?.SetTag("qdrant.collection", CollectionName);
        activity?.SetTag("qdrant.rule_id", ruleId);
        activity?.SetTag("qdrant.embedding_dimensions", embedding.Length);

        try
        {
            // Ensure collection exists before storing
            await EnsureCollectionExistsAsync(cancellationToken);

            // Convert metadata to Qdrant payload
            Dictionary<string, Value> payload = new();
            foreach ((string key, string value) in metadata) payload[key] = new Value { StringValue = value };

            // Generate a point ID from the rule ID string
            ulong qdrantPointId = (ulong)Math.Abs(ruleId.GetHashCode());
            if (qdrantPointId == 0)
                // Avoid zero ID (Qdrant doesn't allow it)
                qdrantPointId = 1;

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

            logger.LogInformation("Stored rule {RuleId} in collection {CollectionName}",
                ruleId,
                CollectionName);

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to store rule {RuleId}", ruleId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public async Task<List<RuleSearchResult>> SearchRulesAsync(
        float[] queryEmbedding,
        string? rulesetId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = activitySource.StartActivity("QdrantRules.Search");
        activity?.SetTag("qdrant.collection", CollectionName);
        activity?.SetTag("qdrant.limit", limit);
        activity?.SetTag("qdrant.ruleset_id", rulesetId ?? "all");

        try
        {
            if (queryEmbedding.Length != EmbeddingDimensions)
                throw new ArgumentException(
                    $"Query embedding has {queryEmbedding.Length} dimensions, expected {EmbeddingDimensions}");

            // Build filter if searching within a specific ruleset
            Filter? filter = null;
            if (!string.IsNullOrWhiteSpace(rulesetId))
                filter = new Filter
                {
                    Must =
                    {
                        new Condition
                        {
                            Field = new FieldCondition
                            {
                                Key = "rulesetId",
                                Match = new Match {Text = rulesetId}
                            }
                        }
                    }
                };

            // Search for similar vectors using Qdrant client
            // The Qdrant.Client SearchAsync returns Task<IReadOnlyList<ScoredPoint>>
            IReadOnlyList<ScoredPoint> searchResults = await qdrantClient.SearchAsync(
                CollectionName,
                queryEmbedding,
                filter,
                limit: (ulong)limit,
                cancellationToken: cancellationToken);

            List<RuleSearchResult> results = new();
            foreach (ScoredPoint point in searchResults)
                try
                {
                    string ruleId = point.Payload.GetValueOrDefault("ruleId")?.StringValue ?? string.Empty;
                    string title = point.Payload.GetValueOrDefault("title")?.StringValue ?? string.Empty;
                    string content = point.Payload.GetValueOrDefault("content")?.StringValue ?? string.Empty;
                    string pointRulesetId = point.Payload.GetValueOrDefault("rulesetId")?.StringValue ?? string.Empty;

                    if (!string.IsNullOrWhiteSpace(ruleId) && !string.IsNullOrWhiteSpace(content))
                        results.Add(new RuleSearchResult
                        {
                            RuleId = ruleId,
                            Title = title,
                            Content = content,
                            RulesetId = pointRulesetId,
                            Score = point.Score
                        });
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to parse rule search result, skipping");
                }

            logger.LogInformation("Found {Count} rules matching query", results.Count);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return results;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to search rules");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public async Task<List<DocumentRuleSearchResult>> SearchDocumentRulesAsync(
        float[] queryEmbedding,
        string? rulesetId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = activitySource.StartActivity("QdrantRules.SearchDocumentRules");
        activity?.SetTag("qdrant.collection", DocumentEmbeddingsCollectionName);
        activity?.SetTag("qdrant.limit", limit);
        activity?.SetTag("qdrant.ruleset_id", rulesetId ?? "all");

        try
        {
            // Build filter if searching within a specific ruleset
            Filter? filter = null;
            if (!string.IsNullOrWhiteSpace(rulesetId))
                filter = new Filter
                {
                    Must =
                    {
                        new Condition
                        {
                            Field = new FieldCondition
                            {
                                Key = "rulesetId",
                                Match = new Match {Text = rulesetId}
                            }
                        }
                    }
                };

            // Search for similar vectors using Qdrant client
            IReadOnlyList<ScoredPoint> searchResults = await qdrantClient.SearchAsync(
                DocumentEmbeddingsCollectionName,
                queryEmbedding,
                filter,
                limit: (ulong)limit,
                cancellationToken: cancellationToken);

            List<DocumentRuleSearchResult> results = new();
            foreach (ScoredPoint point in searchResults)
                try
                {
                    string chunkText = point.Payload.GetValueOrDefault("chunkText")?.StringValue ?? string.Empty;
                    string documentId = point.Payload.GetValueOrDefault("documentId")?.StringValue ?? string.Empty;
                    string chunkId = point.Payload.GetValueOrDefault("chunkId")?.StringValue ?? string.Empty;
                    string fileName = point.Payload.GetValueOrDefault("fileName")?.StringValue ?? string.Empty;
                    string pointRulesetId = point.Payload.GetValueOrDefault("rulesetId")?.StringValue ?? string.Empty;

                    if (!string.IsNullOrWhiteSpace(chunkText) && !string.IsNullOrWhiteSpace(documentId) &&
                        !string.IsNullOrWhiteSpace(chunkId))
                    {
                        // EmbeddingId is the Qdrant point ID - reconstruct from chunkId using the same logic as when storing
                        // This matches the approach used in QdrantEmbeddingStore since we can't directly access NumId from PointId
                        ulong qdrantPointId = QdrantUtilities.GeneratePointId(chunkId);
                        string embeddingId = qdrantPointId.ToString();

                        results.Add(new DocumentRuleSearchResult
                        {
                            Text = chunkText,
                            DocumentId = documentId,
                            DocumentName = fileName,
                            RulesetId = pointRulesetId,
                            EmbeddingId = embeddingId,
                            ChunkId = chunkId,
                            Relevancy = point.Score
                        });
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to parse document rule search result, skipping");
                }

            // Sort by relevancy descending (Qdrant should already return sorted, but ensure it)
            results = results.OrderByDescending(r => r.Relevancy).ToList();

            logger.LogInformation("Found {Count} document rules matching query", results.Count);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return results;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to search document rules");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}