using FastEndpoints;
using MassTransit;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class BackfillEmbeddingsEndpoint : Ep.NoReq.Res<BackfillEmbeddingsResponse>
{
    public required IMongoClient MongoClient { get; set; }
    public required IPublishEndpoint PublishEndpoint { get; set; }

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

        List<string> documentIds = new();
        int queuedCount = 0;

        // Publish messages for each unprocessed document
        foreach (CrackedDocument document in unprocessedDocuments)
        {
            if (string.IsNullOrWhiteSpace(document.Id))
            {
                Logger.LogWarning("Skipping document with empty ID. FilePath: {FilePath}", document.FilePath);
                continue;
            }

            try
            {
                DocumentCrackedMessage message = new()
                {
                    DocumentId = document.Id,
                    FilePath = document.FilePath,
                    FileName = document.FileName,
                    RelativeDirectory = document.RelativeDirectory,
                    FileSize = document.FileSize,
                    PageCount = document.PageCount,
                    CrackedAt = document.CrackedAt
                };

                await PublishEndpoint.Publish(message, ct);
                documentIds.Add(document.Id);
                queuedCount++;

                Logger.LogDebug("Queued document {DocumentId} for embedding: {FilePath}", document.Id, document.FilePath);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to queue document {DocumentId} for embedding: {FilePath}", document.Id, document.FilePath);
            }
        }

        Logger.LogInformation("Successfully queued {QueuedCount} of {TotalCount} unprocessed documents for embedding", 
            queuedCount, unprocessedDocuments.Count);

        await Send.OkAsync(new BackfillEmbeddingsResponse
        {
            DocumentsQueued = queuedCount,
            TotalUnprocessed = unprocessedDocuments.Count,
            DocumentIds = documentIds.ToArray()
        }, cancellation: ct);
    }
}

