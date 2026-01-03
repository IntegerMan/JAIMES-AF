namespace MattEland.Jaimes.ApiService.Endpoints;

/// <summary>
/// Endpoint to list RAG collection statistics including documents, chunks, embeddings, and queries.
/// </summary>
public class ListRagCollectionsEndpoint(IDbContextFactory<JaimesDbContext> dbContextFactory)
    : Ep.NoReq.Res<RagCollectionStatisticsResponse>
{
    public override void Configure()
    {
        Get("/admin/rag-collections");
        AllowAnonymous();
        Description(b => b
            .Produces<RagCollectionStatisticsResponse>()
            .WithTags("Admin")
            .WithSummary("List RAG collection statistics for documents, chunks, embeddings, and queries"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        Logger.LogInformation("Fetching RAG collection statistics");

        await using JaimesDbContext dbContext = await dbContextFactory.CreateDbContextAsync(ct);

        // Get all cracked documents with chunk counts
        List<CrackedDocument> documents = await dbContext.CrackedDocuments
            .AsNoTracking()
            .ToListAsync(ct);

        // Get chunk counts with embedding status grouped by document
        var chunkStats = await dbContext.DocumentChunks
            .AsNoTracking()
            .GroupBy(c => c.DocumentId)
            .Select(g => new
            {
                DocumentId = g.Key,
                TotalChunks = g.Count(),
                EmbeddedChunks = g.Count(c => c.QdrantPointId != null)
            })
            .ToListAsync(ct);

        Dictionary<int, (int Total, int Embedded)> chunksByDocument = chunkStats
            .ToDictionary(x => x.DocumentId, x => (x.TotalChunks, x.EmbeddedChunks));

        // Get query counts by index name (which corresponds to collection type)
        var queryCounts = await dbContext.RagSearchQueries
            .AsNoTracking()
            .GroupBy(q => q.IndexName)
            .Select(g => new { IndexName = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        Dictionary<string, int> queryCountByIndex = queryCounts
            .ToDictionary(x => x.IndexName, x => x.Count, StringComparer.OrdinalIgnoreCase);

        // Build document info list
        List<RagCollectionDocumentInfo> documentInfos = documents
            .Select(doc =>
            {
                chunksByDocument.TryGetValue(doc.Id, out (int Total, int Embedded) chunks);
                return new RagCollectionDocumentInfo
                {
                    DocumentId = doc.Id,
                    FileName = doc.FileName,
                    RelativeDirectory = doc.RelativeDirectory,
                    DocumentKind = doc.DocumentKind,
                    RulesetId = doc.RulesetId,
                    TotalChunks = chunks.Total,
                    EmbeddedChunks = chunks.Embedded,
                    IsFullyProcessed = doc.IsProcessed,
                    CrackedAt = doc.CrackedAt
                };
            })
            .OrderBy(d => d.DocumentKind)
            .ThenBy(d => d.RulesetId)
            .ThenBy(d => d.FileName)
            .ToList();

        // Build summaries by collection type
        var summaryGroups = documentInfos
            .GroupBy(d => d.DocumentKind)
            .Select(g =>
            {
                // Try to find matching query index
                string indexName = g.Key.ToLowerInvariant() switch
                {
                    "sourcebook" => "rules",
                    "transcript" => "conversations",
                    _ => g.Key.ToLowerInvariant()
                };

                queryCountByIndex.TryGetValue(indexName, out int queryCount);

                return new RagCollectionSummary
                {
                    CollectionType = g.Key,
                    DocumentCount = g.Count(),
                    TotalChunks = g.Sum(d => d.TotalChunks),
                    EmbeddedChunks = g.Sum(d => d.EmbeddedChunks),
                    QueryCount = queryCount
                };
            })
            .OrderBy(s => s.CollectionType)
            .ToArray();

        Logger.LogInformation(
            "Returning statistics for {DocumentCount} documents across {CollectionCount} collections",
            documentInfos.Count,
            summaryGroups.Length);

        await Send.OkAsync(new RagCollectionStatisticsResponse
        {
            Summaries = summaryGroups,
            Documents = documentInfos.ToArray()
        }, ct);
    }
}
