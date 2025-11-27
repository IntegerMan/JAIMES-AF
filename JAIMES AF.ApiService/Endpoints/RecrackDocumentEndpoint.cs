using FastEndpoints;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Models;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MongoDB.Driver;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class RecrackDocumentEndpoint : Endpoint<RecrackDocumentRequest, DocumentOperationResponse>
{
    public required IMessagePublisher MessagePublisher { get; set; }
    public required IMongoClient MongoClient { get; set; }

    public override void Configure()
    {
        Post("/documents/recrack");
        AllowAnonymous();
        Description(b => b
            .Produces<DocumentOperationResponse>()
            .WithTags("Documents")
            .WithSummary("Requests that a specific document be re-cracked"));
    }

    public override async Task HandleAsync(RecrackDocumentRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.FilePath))
        {
            ThrowError("File path is required.");
            return;
        }

        // Get rulesetId and documentKind from DocumentMetadata if available, otherwise use defaults
        string rulesetId = "default";
        string documentKind = "Sourcebook";
        
        IMongoDatabase database = MongoClient.GetDatabase("documents");
        IMongoCollection<DocumentMetadata> metadataCollection = database.GetCollection<DocumentMetadata>("documentMetadata");
        FilterDefinition<DocumentMetadata> filter = Builders<DocumentMetadata>.Filter.Eq(x => x.FilePath, req.FilePath);
        DocumentMetadata? metadata = await metadataCollection.Find(filter).FirstOrDefaultAsync(ct);
        
        if (metadata != null && !string.IsNullOrWhiteSpace(metadata.RulesetId))
        {
            rulesetId = metadata.RulesetId;
            documentKind = metadata.DocumentKind;
        }

        CrackDocumentMessage message = new()
        {
            FilePath = req.FilePath,
            RelativeDirectory = req.RelativeDirectory,
            RulesetId = rulesetId,
            DocumentKind = documentKind
        };

        await MessagePublisher.PublishAsync(message, ct);
        Logger.LogInformation("Requested re-crack for document {FilePath}", req.FilePath);

        DocumentOperationResponse response = new()
        {
            Success = true,
            Message = $"Re-crack requested for {req.FilePath}."
        };

        await Send.OkAsync(response, cancellation: ct);
    }
}

