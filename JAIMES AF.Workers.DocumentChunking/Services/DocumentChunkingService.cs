using System.Diagnostics;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.Workers.DocumentChunking.Models;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using Microsoft.EntityFrameworkCore;

namespace MattEland.Jaimes.Workers.DocumentChunking.Services;

public class DocumentChunkingService(
    JaimesDbContext dbContext,
    ITextChunkingStrategy chunkingStrategy,
    IQdrantEmbeddingStore qdrantStore,
    IMessagePublisher messagePublisher,
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

            // Parse document ID as integer
            if (!int.TryParse(message.DocumentId, out int documentId))
            {
                throw new InvalidOperationException($"Invalid document ID: {message.DocumentId}");
            }

            // Step 1: Download document content from PostgreSQL
            string documentContent = await DownloadDocumentContentAsync(documentId, cancellationToken);
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
                logger.LogError("Document {DocumentId} produced no chunks after chunking. This is a failure condition.", 
                    message.DocumentId);
                activity?.SetStatus(ActivityStatusCode.Error, "No chunks produced");
                throw new InvalidOperationException($"Document {message.DocumentId} produced no chunks after chunking. This may indicate a problem with the chunking strategy or document content.");
            }

            // Step 3: Store chunks in PostgreSQL
            await StoreChunksAsync(chunks, documentId, cancellationToken);

            // Step 4: Update CrackedDocument with TotalChunks
            await UpdateDocumentChunkCountAsync(documentId, chunks.Count, cancellationToken);

            // Step 5: Store chunks with embeddings in Qdrant, or queue chunks without embeddings for embedding generation
            int storedCount = 0;
            int queuedCount = 0;
            foreach (TextChunk chunk in chunks)
            {
                try
                {
                    if (chunk.Embedding == null || chunk.Embedding.Length == 0)
                    {
                        // Extract page number from chunk text (looks for "--- Page X ---" markers)
                        int? pageNumber = ExtractPageNumberFromChunkText(chunk.Text);
                        
                        // Chunk has no embedding - queue it for embedding generation
                        ChunkReadyForEmbeddingMessage embeddingMessage = new()
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
                            PageNumber = pageNumber,
                            CrackedAt = message.CrackedAt,
                            TotalChunks = chunks.Count,
                            DocumentKind = message.DocumentKind,
                            RulesetId = message.RulesetId
                        };

                        await messagePublisher.PublishAsync(embeddingMessage, cancellationToken);
                        queuedCount++;

                        logger.LogDebug("Queued chunk {ChunkId} (index {ChunkIndex}) for embedding generation for document {DocumentId}", 
                            chunk.Id, chunk.Index, message.DocumentId);
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
                    logger.LogError(ex, "Failed to process chunk {ChunkId} (index {ChunkIndex}) for document {DocumentId}", 
                        chunk.Id, chunk.Index, message.DocumentId);
                    // Continue processing other chunks even if one fails
                }
            }

            // Step 6: Mark document as processed after all chunks are stored
            await MarkDocumentAsProcessedAsync(documentId, cancellationToken);

            activity?.SetTag("chunking.stored_chunks", storedCount);
            activity?.SetTag("chunking.queued_chunks", queuedCount);
            logger.LogInformation("Successfully processed document {DocumentId}: {StoredCount} chunks stored in Qdrant, {QueuedCount} chunks queued for embedding generation out of {ChunkCount} total chunks", 
                message.DocumentId, storedCount, queuedCount, chunks.Count);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process document chunking for {DocumentId}", message.DocumentId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    private async Task<string> DownloadDocumentContentAsync(int documentId, CancellationToken cancellationToken)
    {
        using Activity? activity = activitySource.StartActivity("DocumentChunking.DownloadDocument");
        activity?.SetTag("chunking.document_id", documentId);

        try
        {
            CrackedDocument? document = await dbContext.CrackedDocuments
                .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);

            if (document == null)
            {
                throw new InvalidOperationException($"Document with ID {documentId} not found in PostgreSQL");
            }

            logger.LogDebug("Downloaded document content for {DocumentId} ({Length} characters)", 
                documentId, document.Content.Length);

            activity?.SetStatus(ActivityStatusCode.Ok);
            return document.Content;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to download document {DocumentId} from PostgreSQL", documentId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    private async Task StoreChunksAsync(List<TextChunk> chunks, int documentId, CancellationToken cancellationToken)
    {
        using Activity? activity = activitySource.StartActivity("DocumentChunking.StoreChunks");
        activity?.SetTag("chunking.document_id", documentId);
        activity?.SetTag("chunking.chunk_count", chunks.Count);

        try
        {
            // Store chunks (using upsert logic to handle duplicates)
            foreach (TextChunk chunk in chunks)
            {
                DocumentChunk? existingChunk = await dbContext.DocumentChunks
                    .FirstOrDefaultAsync(c => c.ChunkId == chunk.Id, cancellationToken);

                if (existingChunk != null)
                {
                    // Update existing chunk
                    existingChunk.DocumentId = documentId;
                    existingChunk.ChunkText = chunk.Text;
                    existingChunk.ChunkIndex = chunk.Index;
                }
                else
                {
                    // Create new chunk
                    DocumentChunk newChunk = new()
                    {
                        ChunkId = chunk.Id,
                        DocumentId = documentId,
                        ChunkText = chunk.Text,
                        ChunkIndex = chunk.Index,
                        CreatedAt = DateTime.UtcNow
                    };
                    dbContext.DocumentChunks.Add(newChunk);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogDebug("Stored {Count} chunks in PostgreSQL for document {DocumentId}", 
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

    private async Task UpdateDocumentChunkCountAsync(int documentId, int totalChunks, CancellationToken cancellationToken)
    {
        using Activity? activity = activitySource.StartActivity("DocumentChunking.UpdateChunkCount");
        activity?.SetTag("chunking.document_id", documentId);
        activity?.SetTag("chunking.total_chunks", totalChunks);

        try
        {
            CrackedDocument? document = await dbContext.CrackedDocuments
                .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);

            if (document == null)
            {
                logger.LogWarning("Document {DocumentId} not found when updating chunk count", documentId);
            }
            else
            {
                document.TotalChunks = totalChunks;
                document.ProcessedChunkCount = 0; // Reset processed count when re-chunking
                await dbContext.SaveChangesAsync(cancellationToken);
                
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

    private async Task MarkDocumentAsProcessedAsync(int documentId, CancellationToken cancellationToken)
    {
        using Activity? activity = activitySource.StartActivity("DocumentChunking.MarkProcessed");
        activity?.SetTag("chunking.document_id", documentId);

        try
        {
            CrackedDocument? document = await dbContext.CrackedDocuments
                .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);

            if (document == null)
            {
                logger.LogWarning("Document {DocumentId} not found when marking as processed", documentId);
            }
            else
            {
                document.IsProcessed = true;
                await dbContext.SaveChangesAsync(cancellationToken);
                
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

    /// <summary>
    /// Extracts the page number from chunk text by looking for "--- Page X ---" markers.
    /// Returns the first page number found, or null if no page marker is found.
    /// </summary>
    private static int? ExtractPageNumberFromChunkText(string chunkText)
    {
        if (string.IsNullOrWhiteSpace(chunkText))
        {
            return null;
        }

        // Look for "--- Page X ---" pattern in the text
        // The pattern is added by PDF extraction: builder.AppendLine($"--- Page {page.Number} ---");
        int pageMarkerIndex = chunkText.IndexOf("--- Page ", StringComparison.Ordinal);
        if (pageMarkerIndex < 0)
        {
            return null;
        }

        // Find the start of the page number (after "--- Page ")
        int numberStart = pageMarkerIndex + 9; // Length of "--- Page "
        
        // Find the end of the page number (before " ---")
        int numberEnd = chunkText.IndexOf(" ---", numberStart, StringComparison.Ordinal);
        if (numberEnd < 0)
        {
            return null;
        }

        // Extract the page number substring
        string pageNumberStr = chunkText.Substring(numberStart, numberEnd - numberStart).Trim();
        
        if (int.TryParse(pageNumberStr, out int pageNumber))
        {
            return pageNumber;
        }

        return null;
    }
}
