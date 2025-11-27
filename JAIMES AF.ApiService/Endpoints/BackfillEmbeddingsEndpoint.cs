using FastEndpoints;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class BackfillEmbeddingsEndpoint : Ep.NoReq.Res<BackfillEmbeddingsResponse>
{
    public required IMongoClient MongoClient { get; set; }
    public required IMessagePublisher MessagePublisher { get; set; }

    public override void Configure()
    {
        Post("/documents/backfill-embeddings");
        AllowAnonymous();
        Description(b => b
            .Produces<BackfillEmbeddingsResponse>()
            .WithTags("Documents")
            .WithSummary("Backfill unprocessed documents for embedding")
            .WithDescription("Finds all unprocessed documents in crackedDocuments collection and queues them for embedding processing."));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        Logger.LogInformation("Starting backfill of unprocessed documents for embedding");

        // Get database and collection
        IMongoDatabase mongoDatabase = MongoClient.GetDatabase("documents");
        IMongoCollection<CrackedDocument> collection = mongoDatabase.GetCollection<CrackedDocument>("crackedDocuments");

        // Find all unprocessed documents
        FilterDefinition<CrackedDocument> filter = Builders<CrackedDocument>.Filter.Eq(d => d.IsProcessed, false);
        List<CrackedDocument> unprocessedDocuments = await collection.Find(filter).ToListAsync(ct);

        Logger.LogInformation("Found {Count} unprocessed documents", unprocessedDocuments.Count);

        // Filter out documents with empty IDs and create publish tasks
        List<(CrackedDocument Document, Task PublishTask)> publishTasks = new();
        
        foreach (CrackedDocument document in unprocessedDocuments)
        {
            if (string.IsNullOrWhiteSpace(document.Id))
            {
                Logger.LogWarning("Skipping document with empty ID. FilePath: {FilePath}", document.FilePath);
                continue;
            }

            DocumentReadyForChunkingMessage message = new()
            {
                DocumentId = document.Id,
                FilePath = document.FilePath,
                FileName = document.FileName,
                RelativeDirectory = document.RelativeDirectory,
                FileSize = document.FileSize,
                PageCount = document.PageCount,
                CrackedAt = document.CrackedAt,
                DocumentKind = document.DocumentKind,
                RulesetId = document.RulesetId
            };

            // Create publish task - don't await yet, we'll await all in parallel
            Task publishTask = MessagePublisher.PublishAsync(message, ct)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted && t.Exception != null)
                    {
                        Logger.LogError(t.Exception.GetBaseException(), "Failed to queue document {DocumentId} for embedding: {FilePath}", 
                            document.Id, document.FilePath);
                    }
                    else if (t.IsCompletedSuccessfully)
                    {
                        Logger.LogDebug("Queued document {DocumentId} for embedding: {FilePath}", document.Id, document.FilePath);
                    }
                    return t;
                }, TaskContinuationOptions.ExecuteSynchronously);

            publishTasks.Add((document, publishTask));
        }

        // Publish all messages in parallel (fire-and-forget)
        Logger.LogInformation("Starting to publish {Count} messages to LavinMQ in parallel (fire-and-forget)", publishTasks.Count);
        
        // Start all publish operations but don't wait for them - return immediately
        // The publishes will continue in the background
        List<string> documentIds = publishTasks
            .Select(t => t.Document.Id!)
            .ToList();
        
        // Fire off all publishes in the background - don't await
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.WhenAll(publishTasks.Select(t => t.PublishTask));
                
                // Count successes after all complete
                int successCount = publishTasks.Count(t => t.PublishTask.IsCompletedSuccessfully);
                int failedCount = publishTasks.Count - successCount;
                
                if (failedCount > 0)
                {
                    Logger.LogWarning("Background publish completed: {SuccessCount} succeeded, {FailedCount} failed out of {TotalCount} documents", 
                        successCount, failedCount, publishTasks.Count);
                }
                else
                {
                    Logger.LogInformation("Background publish completed: Successfully queued all {Count} documents for embedding", 
                        publishTasks.Count);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error during background publish of documents");
            }
        }, ct);
        
        // Return immediately with the count of documents that will be processed
        Logger.LogInformation("Returning immediately. {Count} documents are being queued in the background", documentIds.Count);
        
        await Send.OkAsync(new BackfillEmbeddingsResponse
        {
            DocumentsQueued = documentIds.Count,
            TotalUnprocessed = unprocessedDocuments.Count,
            DocumentIds = documentIds.ToArray()
        }, cancellation: ct);
    }
}

