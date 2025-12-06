namespace MattEland.Jaimes.ApiService.Endpoints;

public class ListDocumentsEndpoint(IDbContextFactory<JaimesDbContext> dbContextFactory)
    : Ep.NoReq.Res<DocumentStatusResponse>
{
    public override void Configure()
    {
        Get("/documents");
        AllowAnonymous();
        Description(b => b
            .Produces<DocumentStatusResponse>()
            .WithTags("Documents")
            .WithSummary("List all detected documents with their processing status"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        Logger.LogInformation("Listing all documents with processing status");

        await using JaimesDbContext dbContext = await dbContextFactory.CreateDbContextAsync(ct);

        // Get all detected documents
        List<DocumentMetadata> allMetadata = await dbContext.DocumentMetadata.ToListAsync(ct);
        Logger.LogInformation("Found {Count} detected documents in DocumentMetadata table", allMetadata.Count);

        // Get all cracked documents for quick lookup
        List<CrackedDocument> allCracked = await dbContext.CrackedDocuments.ToListAsync(ct);
        Logger.LogInformation("Found {Count} cracked documents in CrackedDocuments table", allCracked.Count);

        Dictionary<string, CrackedDocument> crackedByPath = allCracked
            .Where(d => !string.IsNullOrWhiteSpace(d.FilePath))
            .ToDictionary(d => d.FilePath, d => d, StringComparer.OrdinalIgnoreCase);

        // Build response
        List<DocumentStatusInfo> documents = new();
        foreach (DocumentMetadata metadata in allMetadata)
        {
            bool hasCracked = crackedByPath.TryGetValue(metadata.FilePath, out CrackedDocument? cracked);
            bool isCracked = hasCracked && !string.IsNullOrWhiteSpace(cracked?.Content);
            bool hasEmbeddings = hasCracked && cracked?.IsProcessed == true;

            documents.Add(new DocumentStatusInfo
            {
                DocumentId = hasCracked ? cracked!.Id.ToString() : null,
                FilePath = metadata.FilePath,
                FileName = Path.GetFileName(metadata.FilePath),
                RelativeDirectory = hasCracked ? cracked!.RelativeDirectory : string.Empty,
                IsCracked = isCracked,
                HasEmbeddings = hasEmbeddings,
                CrackedAt = hasCracked ? cracked!.CrackedAt : null,
                FileSize = hasCracked ? cracked!.FileSize : null,
                PageCount = hasCracked ? cracked!.PageCount : null,
                DocumentKind = hasCracked ? cracked!.DocumentKind : null,
                RulesetId = hasCracked ? cracked!.RulesetId : null
            });
        }

        // Sort by file path
        documents = documents.OrderBy(d => d.FilePath).ToList();

        Logger.LogInformation("Returning {Count} documents with status information", documents.Count);

        await Send.OkAsync(new DocumentStatusResponse
            {
                Documents = documents.ToArray()
            },
            ct);
    }
}