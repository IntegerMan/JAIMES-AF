namespace MattEland.Jaimes.ApiService.Endpoints;

/// <summary>
/// Endpoint to get detailed information about a specific document chunk.
/// </summary>
public class GetChunkDetailsEndpoint(IDbContextFactory<JaimesDbContext> dbContextFactory)
    : Ep.NoReq.Res<ChunkDetailsResponse>
{
    public override void Configure()
    {
        Get("/admin/chunks/{chunkId}");
        AllowAnonymous();
        Description(b => b
            .Produces<ChunkDetailsResponse>()
            .WithTags("Admin")
            .WithSummary("Get detailed information about a specific document chunk"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        string chunkId = Route<string>("chunkId") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(chunkId))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        Logger.LogInformation("Fetching details for chunk {ChunkId}", chunkId);

        await using JaimesDbContext dbContext = await dbContextFactory.CreateDbContextAsync(ct);

        // Get the chunk with its document
        DocumentChunk? chunk = await dbContext.DocumentChunks
            .AsNoTracking()
            .Include(c => c.CrackedDocument)
            .FirstOrDefaultAsync(c => c.ChunkId == chunkId, ct);

        if (chunk == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        // Get queries that returned this chunk
        List<RagSearchResultChunk> resultChunks = await dbContext.RagSearchResultChunks
            .AsNoTracking()
            .Include(r => r.RagSearchQuery)
            .Where(r => r.ChunkId == chunkId)
            .ToListAsync(ct);

        ChunkQueryAppearance[] queryAppearances = resultChunks
            .Where(r => r.RagSearchQuery != null)
            .Select(r => new ChunkQueryAppearance
            {
                QueryId = r.RagSearchQueryId,
                QueryText = r.RagSearchQuery!.Query,
                Relevancy = r.Relevancy,
                QueryDate = r.RagSearchQuery.CreatedAt
            })
            .OrderByDescending(q => q.QueryDate)
            .ToArray();

        // Build embedding preview and full embedding
        float[]? embeddingPreview = null;
        float[]? fullEmbedding = null;
        int embeddingDimensions = 0;
        if (chunk.Embedding != null)
        {
            ReadOnlyMemory<float> memory = chunk.Embedding.ToArray();
            embeddingDimensions = memory.Length;
            embeddingPreview = memory.Span[..Math.Min(10, memory.Length)].ToArray();
            fullEmbedding = memory.ToArray();
        }

        Logger.LogInformation(
            "Returning details for chunk {ChunkId} with {QueryCount} query appearances",
            chunkId,
            queryAppearances.Length);

        await Send.OkAsync(new ChunkDetailsResponse
            {
                ChunkId = chunk.ChunkId,
                ChunkIndex = chunk.ChunkIndex,
                ChunkText = chunk.ChunkText,
                HasEmbedding = chunk.QdrantPointId != null,
                QdrantPointId = chunk.QdrantPointId,
                EmbeddingPreview = embeddingPreview,
                FullEmbedding = fullEmbedding,
                EmbeddingDimensions = embeddingDimensions,
                CreatedAt = chunk.CreatedAt,
                DocumentId = chunk.DocumentId,
                DocumentName = chunk.CrackedDocument?.FileName ?? "Unknown",
                DocumentKind = chunk.CrackedDocument?.DocumentKind ?? "Unknown",
                RulesetId = chunk.CrackedDocument?.RulesetId ?? "Unknown",
                QueryAppearances = queryAppearances
            },
            ct);
    }
}
