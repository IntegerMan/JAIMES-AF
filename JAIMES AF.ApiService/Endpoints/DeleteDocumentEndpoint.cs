using FastEndpoints;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Models;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MongoDB.Driver;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class DeleteDocumentEndpoint : Endpoint<DeleteDocumentRequest, DocumentOperationResponse>
{
    public required IMongoClient MongoClient { get; set; }

    public override void Configure()
    {
        Post("/documents/delete");
        AllowAnonymous();
        Description(b => b
            .Produces<DocumentOperationResponse>()
            .WithTags("Documents")
            .WithSummary("Deletes a document and associated metadata."));
    }

    public override async Task HandleAsync(DeleteDocumentRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.FilePath))
        {
            ThrowError("File path is required.");
            return;
        }

        IMongoDatabase database = MongoClient.GetDatabase("documents");
        IMongoCollection<DocumentMetadata> metadataCollection = database.GetCollection<DocumentMetadata>("documentMetadata");
        IMongoCollection<CrackedDocument> crackedCollection = database.GetCollection<CrackedDocument>("crackedDocuments");

        FilterDefinition<DocumentMetadata> metadataFilter = Builders<DocumentMetadata>.Filter.Eq(x => x.FilePath, req.FilePath);
        FilterDefinition<CrackedDocument> crackedFilter = Builders<CrackedDocument>.Filter.Eq(x => x.FilePath, req.FilePath);

        DeleteResult metadataResult = await metadataCollection.DeleteOneAsync(metadataFilter, ct);
        DeleteResult crackedResult = await crackedCollection.DeleteOneAsync(crackedFilter, ct);

        Logger.LogInformation(
            "Deleted document {FilePath}. MetadataDeleted={MetadataCount}, CrackedDeleted={CrackedCount}",
            req.FilePath,
            metadataResult.DeletedCount,
            crackedResult.DeletedCount);

        DocumentOperationResponse response = new()
        {
            Success = true,
            Message = $"Deleted document at {req.FilePath}."
        };

        await Send.OkAsync(response, cancellation: ct);
    }
}

