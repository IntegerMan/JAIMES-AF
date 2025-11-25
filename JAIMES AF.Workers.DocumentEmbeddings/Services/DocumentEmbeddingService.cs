using System.Diagnostics;
using MongoDB.Driver;
using Microsoft.Extensions.Logging;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.Workers.DocumentEmbeddings.Configuration;

namespace MattEland.Jaimes.Workers.DocumentEmbeddings.Services;

public class DocumentEmbeddingService(
    IMongoClient mongoClient,
    IOllamaEmbeddingService ollamaEmbeddingService,
    IQdrantEmbeddingStore qdrantStore,
    ILogger<DocumentEmbeddingService> logger,
    ActivitySource activitySource) : IDocumentEmbeddingService
{
    public async Task ProcessChunkAsync(ChunkReadyForEmbeddingMessage message, CancellationToken cancellationToken = default)
    {
        using Activity? activity = activitySource.StartActivity("DocumentEmbedding.ProcessChunk");
        activity?.SetTag("embedding.chunk_id", message.ChunkId);
        activity?.SetTag("embedding.document_id", message.DocumentId);
        activity?.SetTag("embedding.chunk_index", message.ChunkIndex);

        try
        {
            logger.LogInformation("Processing chunk for embedding: {ChunkId} (DocumentId={DocumentId}, Index={ChunkIndex})", 
                message.ChunkId, message.DocumentId, message.ChunkIndex);

            // Ensure collection exists before processing (in case it wasn't created on startup)
            try
            {
                await qdrantStore.EnsureCollectionExistsAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to ensure collection exists during initial check, will retry during storage. Continuing with chunk processing...");
            }

            // Generate embedding for this chunk
            float[] embedding = await ollamaEmbeddingService.GenerateEmbeddingAsync(message.ChunkText, cancellationToken);
            activity?.SetTag("embedding.dimensions", embedding.Length);

            // Prepare metadata for this chunk
            Dictionary<string, string> metadata = new()
            {
                { "chunkId", message.ChunkId },
                { "chunkIndex", message.ChunkIndex.ToString() },
                { "chunkText", message.ChunkText },
                { "documentId", message.DocumentId },
                { "fileName", message.FileName },
                { "filePath", message.FilePath },
                { "relativeDirectory", message.RelativeDirectory },
                { "fileSize", message.FileSize.ToString() },
                { "pageCount", message.PageCount.ToString() },
                { "crackedAt", message.CrackedAt.ToString("O") },
                { "embeddedAt", DateTime.UtcNow.ToString("O") }
            };

            // Store chunk embedding in Qdrant
            await qdrantStore.StoreEmbeddingAsync(message.ChunkId, embedding, metadata, cancellationToken);

            // Increment processed chunk count and check if document is complete
            await IncrementProcessedChunkCountAsync(message.DocumentId, message.TotalChunks, cancellationToken);

            logger.LogDebug("Processed chunk {ChunkId} (index {ChunkIndex}) for document {DocumentId}", 
                message.ChunkId, message.ChunkIndex, message.DocumentId);

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process chunk embedding for {ChunkId}", message.ChunkId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    private async Task IncrementProcessedChunkCountAsync(string documentId, int totalChunks, CancellationToken cancellationToken)
    {
        using Activity? activity = activitySource.StartActivity("DocumentEmbedding.IncrementChunkCount");
        activity?.SetTag("embedding.document_id", documentId);

        try
        {
            // Get database from connection string - the database name is "documents" as configured in AppHost
            IMongoDatabase mongoDatabase = mongoClient.GetDatabase("documents");
            IMongoCollection<CrackedDocument> collection = mongoDatabase.GetCollection<CrackedDocument>("crackedDocuments");

            FilterDefinition<CrackedDocument> filter = Builders<CrackedDocument>.Filter.Eq(d => d.Id, documentId);
            
            // Use atomic increment to avoid race conditions
            UpdateDefinition<CrackedDocument> update = Builders<CrackedDocument>.Update
                .Inc(d => d.ProcessedChunkCount, 1);

            UpdateResult result = await collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

            if (result.MatchedCount == 0)
            {
                logger.LogWarning("Document {DocumentId} not found when incrementing processed chunk count", documentId);
                return;
            }

            // Check if all chunks are processed
            CrackedDocument? document = await collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
            if (document != null && document.ProcessedChunkCount >= totalChunks)
            {
                // Mark document as processed
                UpdateDefinition<CrackedDocument> markProcessedUpdate = Builders<CrackedDocument>.Update
                    .Set(d => d.IsProcessed, true);

                await collection.UpdateOneAsync(filter, markProcessedUpdate, cancellationToken: cancellationToken);
                
                logger.LogInformation("All chunks processed for document {DocumentId}. Marked as processed.", documentId);
            }
            else
            {
                logger.LogDebug("Incremented processed chunk count for document {DocumentId}. Progress: {Processed}/{Total}", 
                    documentId, document?.ProcessedChunkCount ?? 0, totalChunks);
            }

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to increment processed chunk count for document {DocumentId}", documentId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            // Don't throw - this is a non-critical operation, but log the error
        }
    }
}

