namespace MattEland.Jaimes.ApiService.Endpoints;

public class DeleteDocumentEndpoint(IDbContextFactory<JaimesDbContext> dbContextFactory)
    : Endpoint<DeleteDocumentRequest, DocumentOperationResponse>
{
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

        await using JaimesDbContext dbContext = await dbContextFactory.CreateDbContextAsync(ct);

        // Find and delete metadata
        DocumentMetadata? metadata = await dbContext.DocumentMetadata
            .FirstOrDefaultAsync(x => x.FilePath == req.FilePath, ct);

        int metadataDeletedCount = 0;
        if (metadata != null)
        {
            dbContext.DocumentMetadata.Remove(metadata);
            metadataDeletedCount = 1;
        }

        // Find and delete cracked document (this will cascade delete chunks due to FK relationship)
        CrackedDocument? crackedDocument = await dbContext.CrackedDocuments
            .FirstOrDefaultAsync(x => x.FilePath == req.FilePath, ct);

        int crackedDeletedCount = 0;
        if (crackedDocument != null)
        {
            dbContext.CrackedDocuments.Remove(crackedDocument);
            crackedDeletedCount = 1;
        }

        await dbContext.SaveChangesAsync(ct);

        Logger.LogInformation(
            "Deleted document {FilePath}. MetadataDeleted={MetadataCount}, CrackedDeleted={CrackedCount}",
            req.FilePath,
            metadataDeletedCount,
            crackedDeletedCount);

        DocumentOperationResponse response = new()
        {
            Success = true,
            Message = $"Deleted document at {req.FilePath}."
        };

        await Send.OkAsync(response, ct);
    }
}