using FastEndpoints;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MongoDB.Driver;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class QueueDocumentEmbeddingEndpoint : Endpoint<QueueDocumentEmbeddingRequest, DocumentOperationResponse>
{
    public required IMongoClient MongoClient { get; set; }
    public required IMessagePublisher MessagePublisher { get; set; }

    public override void Configure()
    {
        Post("/documents/queue-embedding");
        AllowAnonymous();
        Description(b => b
            .Produces<DocumentOperationResponse>()
            .WithTags("Documents")
            .WithSummary("Queues a cracked document for embedding generation."));
    }

    public override async Task HandleAsync(QueueDocumentEmbeddingRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.DocumentId))
        {
            ThrowError("Document ID is required.");
            return;
        }

        IMongoDatabase database = MongoClient.GetDatabase("documents");
        IMongoCollection<CrackedDocument> crackedCollection = database.GetCollection<CrackedDocument>("crackedDocuments");

        FilterDefinition<CrackedDocument> filter = Builders<CrackedDocument>.Filter.Eq(d => d.Id, req.DocumentId);
        CrackedDocument? document = await crackedCollection.Find(filter).FirstOrDefaultAsync(ct);

        if (document == null || string.IsNullOrWhiteSpace(document.Id))
        {
            await Send.NotFoundAsync(cancellation: ct);
            return;
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

        await crackedCollection.UpdateOneAsync(
            filter,
            Builders<CrackedDocument>.Update.Set(d => d.IsProcessed, false),
            cancellationToken: ct);

        await MessagePublisher.PublishAsync(message, ct);
        Logger.LogInformation("Queued embeddings for document {DocumentId} ({FilePath})", document.Id, document.FilePath);

        DocumentOperationResponse response = new()
        {
            Success = true,
            Message = $"Queued embeddings for {document.FileName}."
        };

        await Send.OkAsync(response, cancellation: ct);
    }
}

