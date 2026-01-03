using MattEland.Jaimes.ServiceDefinitions.Requests;

namespace MattEland.Jaimes.ApiService.Endpoints;

/// <summary>
/// Endpoint to list RAG search queries for a specific collection.
/// </summary>
public class ListRagSearchQueriesEndpoint(IDbContextFactory<JaimesDbContext> dbContextFactory)
    : Endpoint<RagSearchQueriesRequest, RagSearchQueriesResponse>
{
    public override void Configure()
    {
        Get("/admin/rag-collections/{IndexName}/queries");
        AllowAnonymous();
        Description(b => b
            .Produces<RagSearchQueriesResponse>()
            .WithTags("Admin")
            .WithSummary("List RAG search queries for a specific collection index"));
    }

    public override async Task HandleAsync(RagSearchQueriesRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.IndexName))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        // Normalize index name for comparison and handle mapping to storage names
        string normalizedIndex = req.IndexName.ToLowerInvariant();
        List<string> targetIndices = normalizedIndex switch
        {
            "rules" => ["rules", "document-embeddings", "rulesets"],
            "conversations" => ["conversations"],
            _ => [normalizedIndex]
        };

        // Fix pagination values
        if (req.Page < 1) req.Page = 1;
        if (req.PageSize < 1) req.PageSize = 1;
        if (req.PageSize > 100) req.PageSize = 100;

        Logger.LogInformation(
            "Fetching RAG search queries for index {IndexName} (Targeting: {TargetIndices}), page {Page}, pageSize {PageSize}, documentName {DocumentName}",
            normalizedIndex,
            string.Join(", ", targetIndices),
            req.Page,
            req.PageSize,
            req.DocumentName ?? "(all)");

        await using JaimesDbContext dbContext = await dbContextFactory.CreateDbContextAsync(ct);

        // Build the base query - optionally filter by document name
        IQueryable<RagSearchQuery> baseQuery = dbContext.RagSearchQueries
            .AsNoTracking()
            .Include(q => q.ResultChunks)
            .Where(q => targetIndices.Contains(q.IndexName.ToLower()));

        // If filtering by document, only include queries that have results from that document
        if (!string.IsNullOrWhiteSpace(req.DocumentName))
        {
            string normalizedDoc = req.DocumentName.ToLower();
            baseQuery = baseQuery.Where(q => q.ResultChunks.Any(r => r.DocumentName.ToLower() == normalizedDoc));
        }

        // Get total count for pagination
        int totalCount = await baseQuery.CountAsync(ct);

        // Get paginated queries with their results
        List<RagSearchQuery> queries = await baseQuery
            .OrderByDescending(q => q.CreatedAt)
            .Skip((req.Page - 1) * req.PageSize)
            .Take(req.PageSize)
            .ToListAsync(ct);

        // Map to response DTOs
        RagSearchQueryInfo[] queryInfos = queries.Select(q => new RagSearchQueryInfo
            {
                Id = q.Id,
                Query = q.Query,
                CreatedAt = q.CreatedAt,
                RulesetId = q.RulesetId,
                FilterJson = q.FilterJson,
                ResultCount = q.ResultChunks.Count,
                Results = q.ResultChunks.Select(r => new RagSearchResultInfo
                    {
                        DocumentName = r.DocumentName,
                        ChunkId = r.ChunkId,
                        DocumentId = r.DocumentId,
                        RulesetId = r.RulesetId,
                        Relevancy = r.Relevancy
                    })
                    .OrderByDescending(r => r.Relevancy)
                    .ToArray()
            })
            .ToArray();

        // Determine display name
        string displayName = normalizedIndex switch
        {
            "rules" => "Sourcebook Collection",
            "conversations" => "Transcript Collection",
            _ => $"{req.IndexName} Collection"
        };

        Logger.LogInformation("Returning {QueryCount} queries out of {TotalCount} total for index {IndexName}",
            queryInfos.Length,
            totalCount,
            normalizedIndex);

        await Send.OkAsync(new RagSearchQueriesResponse
            {
                IndexName = normalizedIndex,
                CollectionDisplayName = displayName,
                TotalCount = totalCount,
                Page = req.Page,
                PageSize = req.PageSize,
                Queries = queryInfos
            },
            ct);
    }
}
