using System.Diagnostics;
using MongoDB.Driver;
using Microsoft.Extensions.Logging;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.Workers.DocumentChunking.Configuration;
using MattEland.Jaimes.Workers.DocumentChunking.Models;

namespace MattEland.Jaimes.Workers.DocumentChunking.Services;

public class DocumentChunkingService(
    IMongoClient mongoClient,
    ITextChunkingStrategy chunkingStrategy,
    IQdrantEmbeddingStore qdrantStore,
    ILogger<DocumentChunkingService> logger,
    ActivitySource activitySource) : IDocumentChunkingService
{
    public async Task ProcessDocumentAsync(DocumentReadyForChunkingMessage message, CancellationToken cancellationToken = default)
    {
        using Activity? activity = activitySource.StartActivity("DocumentChunking.ProcessDocument");
        activity?.SetTag("chunking.document_id", message.DocumentId);
        activity?.SetTag("chunking.file_name", message.FileName);
        activity?.SetTag("chunking.file_path", message.FilePath);

        try
        {
            logger.LogDebug("Processing document for chunking: {DocumentId} ({FileName})", 
                message.DocumentId, message.FileName);

            // Step 1: Download document content from MongoDB
            string documentContent = await DownloadDocumentContentAsync(message.DocumentId, cancellationToken);
            activity?.SetTag("chunking.content_length", documentContent.Length);

            if (string.IsNullOrWhiteSpace(documentContent))
            {
                logger.LogWarning("Document {DocumentId} has empty content, skipping chunking", 
                    message.DocumentId);
                activity?.SetStatus(ActivityStatusCode.Error, "Empty document content");
                return;
            }

            // Step 2: Chunk the document
            logger.LogInformation("Starting chunking for document {DocumentId}. Content length: {ContentLength}", 
                message.DocumentId, documentContent.Length);
            activity?.SetTag("chunking.content_length", documentContent.Length);
            
            List<TextChunk> chunks;
            try
            {
                chunks = chunkingStrategy.ChunkText(documentContent, message.DocumentId).ToList();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to chunk document {DocumentId}. This may be due to embedding generation issues.", 
                    message.DocumentId);
                activity?.SetStatus(ActivityStatusCode.Error, $"Chunking failed: {ex.Message}");
                throw;
            }
            
            activity?.SetTag("chunking.chunk_count", chunks.Count);
            logger.LogInformation("Document {DocumentId} split into {ChunkCount} chunks", message.DocumentId, chunks.Count);

            if (chunks.Count == 0)
            {
                logger.LogWarning("Document {DocumentId} produced no chunks, skipping", 
                    message.DocumentId);
                activity?.SetStatus(ActivityStatusCode.Error, "No chunks produced");
                return;
            }

            // Step 3: Store chunks in MongoDB
            await StoreChunksAsync(chunks, message.DocumentId, cancellationToken);

            // Step 4: Update CrackedDocument with TotalChunks
            await UpdateDocumentChunkCountAsync(message.DocumentId, chunks.Count, cancellationToken);

            // Step 5: Store chunks with embeddings in Qdrant
            int storedCount = 0;
            foreach (TextChunk chunk in chunks)
            {
                try
                {
                    if (chunk.Embedding == null || chunk.Embedding.Length == 0)
                    {
                        logger.LogWarning("Chunk {ChunkId} (index {ChunkIndex}) has no embedding, skipping Qdrant storage", 
                            chunk.Id, chunk.Index);
                        continue;
                    }

                    // Prepare metadata for this chunk
                    Dictionary<string, string> metadata = new()
                    {
                        { "chunkId", chunk.Id },
                        { "chunkIndex", chunk.Index.ToString() },
                        { "chunkText", chunk.Text },
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
                    await qdrantStore.StoreEmbeddingAsync(chunk.Id, chunk.Embedding, metadata, cancellationToken);
                    storedCount++;

                    logger.LogDebug("Stored chunk {ChunkId} (index {ChunkIndex}) in Qdrant for document {DocumentId}", 
                        chunk.Id, chunk.Index, message.DocumentId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to store chunk {ChunkId} (index {ChunkIndex}) in Qdrant for document {DocumentId}", 
                        chunk.Id, chunk.Index, message.DocumentId);
                    // Continue storing other chunks even if one fails
                }
            }

            // Step 6: Mark document as processed after all chunks are stored
            await MarkDocumentAsProcessedAsync(message.DocumentId, cancellationToken);

            activity?.SetTag("chunking.stored_chunks", storedCount);
            logger.LogInformation("Successfully processed and stored {StoredCount}/{ChunkCount} chunks for document {DocumentId}", 
                storedCount, chunks.Count, message.DocumentId);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process document chunking for {DocumentId}", message.DocumentId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    private async Task<string> DownloadDocumentContentAsync(string documentId, CancellationToken cancellationToken)
    {
        using Activity? activity = activitySource.StartActivity("DocumentChunking.DownloadDocument");
        activity?.SetTag("chunking.document_id", documentId);

        try
        {
            // Get database from connection string - the database name is "documents" as configured in AppHost
            IMongoDatabase mongoDatabase = mongoClient.GetDatabase("documents");
            IMongoCollection<CrackedDocument> collection = mongoDatabase.GetCollection<CrackedDocument>("crackedDocuments");

            FilterDefinition<CrackedDocument> filter = Builders<CrackedDocument>.Filter.Eq(d => d.Id, documentId);
            CrackedDocument? document = await collection.Find(filter).FirstOrDefaultAsync(cancellationToken);

            if (document == null)
            {
                throw new InvalidOperationException($"Document with ID {documentId} not found in MongoDB");
            }

            logger.LogDebug("Downloaded document content for {DocumentId} ({Length} characters)", 
                documentId, document.Content.Length);

            activity?.SetStatus(ActivityStatusCode.Ok);
            return document.Content;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to download document {DocumentId} from MongoDB", documentId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    private async Task StoreChunksAsync(List<TextChunk> chunks, string documentId, CancellationToken cancellationToken)
    {
        using Activity? activity = activitySource.StartActivity("DocumentChunking.StoreChunks");
        activity?.SetTag("chunking.document_id", documentId);
        activity?.SetTag("chunking.chunk_count", chunks.Count);

        try
        {
            IMongoDatabase mongoDatabase = mongoClient.GetDatabase("documents");
            IMongoCollection<DocumentChunk> collection = mongoDatabase.GetCollection<DocumentChunk>("documentChunks");

            // Ensure indexes exist
            await collection.Indexes.CreateOneAsync(
                new CreateIndexModel<DocumentChunk>(
                    Builders<DocumentChunk>.IndexKeys.Ascending(x => x.DocumentId),
                    new CreateIndexOptions { Name = "idx_documentId" }),
                cancellationToken: cancellationToken);

            await collection.Indexes.CreateOneAsync(
                new CreateIndexModel<DocumentChunk>(
                    Builders<DocumentChunk>.IndexKeys.Ascending(x => x.ChunkId),
                    new CreateIndexOptions { Unique = true, Name = "idx_chunkId_unique" }),
                cancellationToken: cancellationToken);

            // Store chunks (using upsert to handle duplicates)
            // Use UpdateOneAsync instead of ReplaceOneAsync to avoid _id: null issues
            foreach (TextChunk chunk in chunks)
            {
                FilterDefinition<DocumentChunk> filter = Builders<DocumentChunk>.Filter.Eq(c => c.ChunkId, chunk.Id);
                UpdateDefinition<DocumentChunk> update = Builders<DocumentChunk>.Update
                    .Set(c => c.ChunkId, chunk.Id)
                    .Set(c => c.DocumentId, documentId)
                    .Set(c => c.ChunkText, chunk.Text)
                    .Set(c => c.ChunkIndex, chunk.Index)
                    .SetOnInsert(c => c.CreatedAt, DateTime.UtcNow);

                await collection.UpdateOneAsync(
                    filter,
                    update,
                    new UpdateOptions { IsUpsert = true },
                    cancellationToken);
            }

            logger.LogDebug("Stored {Count} chunks in MongoDB for document {DocumentId}", 
                chunks.Count, documentId);

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to store chunks for document {DocumentId}", documentId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    private async Task UpdateDocumentChunkCountAsync(string documentId, int totalChunks, CancellationToken cancellationToken)
    {
        using Activity? activity = activitySource.StartActivity("DocumentChunking.UpdateChunkCount");
        activity?.SetTag("chunking.document_id", documentId);
        activity?.SetTag("chunking.total_chunks", totalChunks);

        try
        {
            IMongoDatabase mongoDatabase = mongoClient.GetDatabase("documents");
            IMongoCollection<CrackedDocument> collection = mongoDatabase.GetCollection<CrackedDocument>("crackedDocuments");

            FilterDefinition<CrackedDocument> filter = Builders<CrackedDocument>.Filter.Eq(d => d.Id, documentId);
            UpdateDefinition<CrackedDocument> update = Builders<CrackedDocument>.Update
                .Set(d => d.TotalChunks, totalChunks)
                .Set(d => d.ProcessedChunkCount, 0); // Reset processed count when re-chunking

            UpdateResult result = await collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

            if (result.MatchedCount == 0)
            {
                logger.LogWarning("Document {DocumentId} not found when updating chunk count", documentId);
            }
            else
            {
                logger.LogDebug("Updated document {DocumentId} with TotalChunks={TotalChunks}", 
                    documentId, totalChunks);
            }

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update chunk count for document {DocumentId}", documentId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            // Don't throw - this is a non-critical operation
        }
    }

    private async Task MarkDocumentAsProcessedAsync(string documentId, CancellationToken cancellationToken)
    {
        using Activity? activity = activitySource.StartActivity("DocumentChunking.MarkProcessed");
        activity?.SetTag("chunking.document_id", documentId);

        try
        {
            IMongoDatabase mongoDatabase = mongoClient.GetDatabase("documents");
            IMongoCollection<CrackedDocument> collection = mongoDatabase.GetCollection<CrackedDocument>("crackedDocuments");

            FilterDefinition<CrackedDocument> filter = Builders<CrackedDocument>.Filter.Eq(d => d.Id, documentId);
            UpdateDefinition<CrackedDocument> update = Builders<CrackedDocument>.Update
                .Set(d => d.IsProcessed, true);

            UpdateResult result = await collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

            if (result.MatchedCount == 0)
            {
                logger.LogWarning("Document {DocumentId} not found when marking as processed", documentId);
            }
            else
            {
                logger.LogDebug("Marked document {DocumentId} as processed", documentId);
            }

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to mark document {DocumentId} as processed", documentId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            // Don't throw - this is a non-critical operation
        }
    }
}

