namespace MattEland.Jaimes.ApiService.Endpoints;

/// <summary>
/// Endpoint to retrieve the stored file content for a sourcebook document.
/// </summary>
public class GetSourcebookDocumentFileEndpoint(IDbContextFactory<JaimesDbContext> dbContextFactory)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/admin/rag-documents/{documentId}/file");
        AllowAnonymous();
        Description(b => b
            .Produces(200, contentType: "application/pdf")
            .Produces(404)
            .WithTags("Admin")
            .WithSummary("Get the PDF file content for a sourcebook document"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        int documentId = Route<int>("documentId");

        Logger.LogInformation("Fetching document file for DocumentId: {DocumentId}", documentId);

        await using JaimesDbContext dbContext = await dbContextFactory.CreateDbContextAsync(ct);

        // Get the cracked document with its stored file
        CrackedDocument? document = await dbContext.CrackedDocuments
            .AsNoTracking()
            .Include(d => d.StoredFile)
            .FirstOrDefaultAsync(d => d.Id == documentId, ct);

        if (document == null)
        {
            Logger.LogWarning("Document not found: {DocumentId}", documentId);
            await Send.NotFoundAsync(ct);
            return;
        }

        if (document.StoredFile == null || document.StoredFile.BinaryContent == null)
        {
            Logger.LogWarning("Document {DocumentId} does not have a stored file", documentId);
            await Send.NotFoundAsync(ct);
            return;
        }

        Logger.LogInformation("Returning document file: {FileName} ({Size} bytes)",
            document.FileName, document.StoredFile.BinaryContent.Length);

        // Set response headers for PDF content
        HttpContext.Response.ContentType = "application/pdf";
        HttpContext.Response.Headers.ContentDisposition = $"inline; filename=\"{document.FileName}\"";

        // Write the file bytes to the response
        await HttpContext.Response.Body.WriteAsync(document.StoredFile.BinaryContent, ct);
    }
}
