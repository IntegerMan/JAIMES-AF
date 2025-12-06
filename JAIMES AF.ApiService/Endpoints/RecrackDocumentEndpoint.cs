using MattEland.Jaimes.ServiceDefinitions.Messages;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class RecrackDocumentEndpoint(
    IMessagePublisher messagePublisher,
    IDbContextFactory<JaimesDbContext> dbContextFactory) : Endpoint<RecrackDocumentRequest, DocumentOperationResponse>
{
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
        string documentKind = DocumentKinds.Sourcebook;

        await using JaimesDbContext dbContext = await dbContextFactory.CreateDbContextAsync(ct);
        DocumentMetadata? metadata = await dbContext.DocumentMetadata
            .FirstOrDefaultAsync(x => x.FilePath == req.FilePath, ct);

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

        await messagePublisher.PublishAsync(message, ct);
        Logger.LogInformation("Requested re-crack for document {FilePath}", req.FilePath);

        DocumentOperationResponse response = new()
        {
            Success = true,
            Message = $"Re-crack requested for {req.FilePath}."
        };

        await Send.OkAsync(response, ct);
    }
}