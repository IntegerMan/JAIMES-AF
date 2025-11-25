using System.Diagnostics;
using MongoDB.Driver;
using Microsoft.Extensions.Logging;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.Workers.DocumentChunking.Configuration;
using MattEland.Jaimes.Workers.DocumentChunking.Models;
using MassTransit;

namespace MattEland.Jaimes.Workers.DocumentChunking.Services;

public class DocumentChunkingService(
    IMongoClient mongoClient,
    ITextChunkingStrategy chunkingStrategy,
    IPublishEndpoint publishEndpoint,
    ILogger<DocumentChunkingService> logger,
    ActivitySource activitySource) : IDocumentChunkingService
{
    public async Task ProcessDocumentAsync(DocumentCrackedMessage message, CancellationToken cancellationToken = default)
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
            List<TextChunk> chunks = chunkingStrategy.ChunkText(documentContent, message.DocumentId).ToList();
            activity?.SetTag("chunking.chunk_count", chunks.Count);
            logger.LogDebug("Document {DocumentId} split into {ChunkCount} chunks", message.DocumentId, chunks.Count);

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

            // Step 5: Publish one message per chunk
            int publishedCount = 0;
            foreach (TextChunk chunk in chunks)
            {
                try
                {
                    ChunkReadyForEmbeddingMessage chunkMessage = new()
                    {
                        ChunkId = chunk.Id,
                        ChunkText = chunk.Text,
                        ChunkIndex = chunk.Index,
                        DocumentId = message.DocumentId,
                        FileName = message.FileName,
                        FilePath = message.FilePath,
                        RelativeDirectory = message.RelativeDirectory,
                        FileSize = message.FileSize,
                        PageCount = message.PageCount,
                        CrackedAt = message.CrackedAt,
                        TotalChunks = chunks.Count
                    };

                    await publishEndpoint.Publish(chunkMessage, cancellationToken);
                    publishedCount++;

                    logger.LogDebug("Published chunk {ChunkId} (index {ChunkIndex}) for document {DocumentId}", 
                        chunk.Id, chunk.Index, message.DocumentId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to publish chunk {ChunkId} (index {ChunkIndex}) for document {DocumentId}", 
                        chunk.Id, chunk.Index, message.DocumentId);
                    // Continue publishing other chunks even if one fails
                }
            }

            activity?.SetTag("chunking.published_chunks", publishedCount);
            logger.LogDebug("Successfully processed {ChunkCount} chunks for document {DocumentId}", 
                chunks.Count, message.DocumentId);
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

            // Convert TextChunk to DocumentChunk entities
            List<DocumentChunk> documentChunks = chunks.Select(chunk => new DocumentChunk
            {
                ChunkId = chunk.Id,
                DocumentId = documentId,
                ChunkText = chunk.Text,
                ChunkIndex = chunk.Index,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            // Insert chunks (using upsert to handle duplicates)
            foreach (DocumentChunk chunk in documentChunks)
            {
                FilterDefinition<DocumentChunk> filter = Builders<DocumentChunk>.Filter.Eq(c => c.ChunkId, chunk.ChunkId);
                await collection.ReplaceOneAsync(
                    filter,
                    chunk,
                    new ReplaceOptions { IsUpsert = true },
                    cancellationToken);
            }

            logger.LogDebug("Stored {Count} chunks in MongoDB for document {DocumentId}", 
                documentChunks.Count, documentId);

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
}

