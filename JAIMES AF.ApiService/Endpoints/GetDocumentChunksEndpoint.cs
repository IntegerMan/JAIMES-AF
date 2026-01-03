namespace MattEland.Jaimes.ApiService.Endpoints;

/// <summary>
/// Endpoint to list document chunks for a specific cracked document.
/// </summary>
public class GetDocumentChunksEndpoint(IDbContextFactory<JaimesDbContext> dbContextFactory)
    : Ep.NoReq.Res<DocumentChunksResponse>
{
    public override void Configure()
    {
        Get("/admin/documents/{documentId:int}/chunks");
        AllowAnonymous();
        Description(b => b
            .Produces<DocumentChunksResponse>()
            .WithTags("Admin")
            .WithSummary("List chunks for a specific document with pagination"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        int documentId = Route<int>("documentId");
        int page = Query<int?>("page") ?? 1;
        int pageSize = Query<int?>("pageSize") ?? 25;

        Logger.LogInformation(
            "Fetching chunks for document {DocumentId}, page {Page}, pageSize {PageSize}",
            documentId, page, pageSize);

        await using JaimesDbContext dbContext = await dbContextFactory.CreateDbContextAsync(ct);

        // Get the document
        CrackedDocument? document = await dbContext.CrackedDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId, ct);

        if (document == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        // Get total count
        int totalCount = await dbContext.DocumentChunks
            .AsNoTracking()
            .CountAsync(c => c.DocumentId == documentId, ct);

        // Get paginated chunks
        List<DocumentChunk> chunks = await dbContext.DocumentChunks
            .AsNoTracking()
            .Where(c => c.DocumentId == documentId)
            .OrderBy(c => c.ChunkIndex)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // Map to response
        DocumentChunkInfo[] chunkInfos = chunks.Select(c => new DocumentChunkInfo
        {
            ChunkId = c.ChunkId,
            ChunkIndex = c.ChunkIndex,
            ChunkTextPreview = c.ChunkText.Length > 200 
                ? c.ChunkText[..200] + "..." 
                : c.ChunkText,
            HasEmbedding = c.QdrantPointId != null,
            CreatedAt = c.CreatedAt
        }).ToArray();

        Logger.LogInformation(
            "Returning {ChunkCount} chunks out of {TotalCount} for document {DocumentId}",
            chunkInfos.Length, totalCount, documentId);

        await Send.OkAsync(new DocumentChunksResponse
        {
            DocumentId = documentId,
            DocumentName = document.FileName,
            DocumentKind = document.DocumentKind,
            RulesetId = document.RulesetId,
            Chunks = chunkInfos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        }, ct);
    }
}
