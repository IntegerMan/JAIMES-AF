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
    public async Task ProcessDocumentAsync(DocumentCrackedMessage message, CancellationToken cancellationToken = default)
    {
        using Activity? activity = activitySource.StartActivity("DocumentEmbedding.ProcessDocument");
        activity?.SetTag("embedding.document_id", message.DocumentId);
        activity?.SetTag("embedding.file_name", message.FileName);
        activity?.SetTag("embedding.file_path", message.FilePath);

        try
        {
            logger.LogInformation("Processing document for embedding: {DocumentId} ({FileName})", 
                message.DocumentId, message.FileName);

            // Ensure collection exists before processing (in case it wasn't created on startup)
            // Note: StoreEmbeddingAsync will also ensure collection exists as a safety net
            try
            {
                await qdrantStore.EnsureCollectionExistsAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to ensure collection exists during initial check, will retry during storage. Continuing with document processing...");
            }

            // Step 1: Download document content from MongoDB
            string documentContent = await DownloadDocumentContentAsync(message.DocumentId, cancellationToken);
            activity?.SetTag("embedding.content_length", documentContent.Length);

            if (string.IsNullOrWhiteSpace(documentContent))
            {
                logger.LogWarning("Document {DocumentId} has empty content, skipping embedding generation", 
                    message.DocumentId);
                activity?.SetStatus(ActivityStatusCode.Error, "Empty document content");
                return;
            }

            // Step 2: Generate embedding using Ollama
            float[] embedding = await ollamaEmbeddingService.GenerateEmbeddingAsync(documentContent, cancellationToken);
            activity?.SetTag("embedding.dimensions", embedding.Length);

            // Step 3: Prepare metadata
            Dictionary<string, string> metadata = new()
            {
                { "documentId", message.DocumentId },
                { "fileName", message.FileName },
                { "filePath", message.FilePath },
                { "relativeDirectory", message.RelativeDirectory },
                { "fileSize", message.FileSize.ToString() },
                { "pageCount", message.PageCount.ToString() },
                { "crackedAt", message.CrackedAt.ToString("O") },
                { "embeddedAt", DateTime.UtcNow.ToString("O") }
            };

            // Step 4: Store embedding in Qdrant
            await qdrantStore.StoreEmbeddingAsync(message.DocumentId, embedding, metadata, cancellationToken);

            // Step 5: Mark document as processed in crackedDocuments collection
            await MarkDocumentAsProcessedAsync(message.DocumentId, cancellationToken);

            logger.LogInformation("Successfully processed embedding for document {DocumentId}", message.DocumentId);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process document embedding for {DocumentId}", message.DocumentId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    private async Task<string> DownloadDocumentContentAsync(string documentId, CancellationToken cancellationToken)
    {
        using Activity? activity = activitySource.StartActivity("DocumentEmbedding.DownloadDocument");
        activity?.SetTag("embedding.document_id", documentId);

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

    private async Task MarkDocumentAsProcessedAsync(string documentId, CancellationToken cancellationToken)
    {
        using Activity? activity = activitySource.StartActivity("DocumentEmbedding.MarkAsProcessed");
        activity?.SetTag("embedding.document_id", documentId);

        try
        {
            // Get database from connection string - the database name is "documents" as configured in AppHost
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

