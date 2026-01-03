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
            .Select(g => new {IndexName = g.Key, Count = g.Count()})
            .ToListAsync(ct);

        Dictionary<string, int> queryCountByIndex = queryCounts
            .ToDictionary(x => x.IndexName, x => x.Count, StringComparer.OrdinalIgnoreCase);

        // Build document info list for sourcebooks
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

        // Get conversation/transcript statistics from MessageEmbeddings
        // Group by game to show each game as a "document" in the transcript collection
        var gameMessageStats = await dbContext.Messages
            .AsNoTracking()
            .Include(m => m.Game)
            .GroupBy(m => new {m.GameId, GameTitle = m.Game!.Title})
            .Select(g => new
            {
                g.Key.GameId,
                g.Key.GameTitle,
                TotalMessages = g.Count()
            })
            .ToListAsync(ct);

        // Get embedding counts per game
        var embeddingCounts = await dbContext.MessageEmbeddings
            .AsNoTracking()
            .Include(e => e.Message)
            .GroupBy(e => e.Message!.GameId)
            .Select(g => new {GameId = g.Key, EmbeddedCount = g.Count()})
            .ToListAsync(ct);

        Dictionary<Guid, int> embeddingsByGame = embeddingCounts
            .ToDictionary(x => x.GameId, x => x.EmbeddedCount);

        // Add game transcripts as documents
        foreach (var game in gameMessageStats)
        {
            embeddingsByGame.TryGetValue(game.GameId, out int embeddedCount);
            documentInfos.Add(new RagCollectionDocumentInfo
            {
                DocumentId = 0, // Games use GUID, not int ID
                FileName = game.GameTitle ?? $"Game {game.GameId}",
                RelativeDirectory = game.GameId.ToString(),
                DocumentKind = DocumentKinds.Transcript,
                RulesetId = "conversations",
                TotalChunks = game.TotalMessages,
                EmbeddedChunks = embeddedCount,
                IsFullyProcessed = embeddedCount >= game.TotalMessages,
                CrackedAt = DateTime.UtcNow // Transcripts don't have a crack date
            });
        }

        // Re-sort after adding transcripts
        documentInfos = documentInfos
            .OrderBy(d => d.DocumentKind)
            .ThenBy(d => d.RulesetId)
            .ThenBy(d => d.FileName)
            .ToList();

        // Build summaries by collection type
        List<RagCollectionSummary> summaries = documentInfos
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
            .ToList();

        Logger.LogInformation(
            "Returning statistics for {DocumentCount} documents across {CollectionCount} collections",
            documentInfos.Count,
            summaries.Count);

        await Send.OkAsync(new RagCollectionStatisticsResponse
            {
                Summaries = summaries.ToArray(),
                Documents = documentInfos.ToArray()
            },
            ct);
    }
}
