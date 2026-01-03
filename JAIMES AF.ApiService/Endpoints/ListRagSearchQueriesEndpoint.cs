namespace MattEland.Jaimes.ApiService.Endpoints;

/// <summary>
/// Endpoint to list RAG search queries for a specific collection.
/// </summary>
public class ListRagSearchQueriesEndpoint(IDbContextFactory<JaimesDbContext> dbContextFactory)
    : Ep.NoReq.Res<RagSearchQueriesResponse>
{
    public override void Configure()
    {
        Get("/admin/rag-collections/{indexName}/queries");
        AllowAnonymous();
        Description(b => b
            .Produces<RagSearchQueriesResponse>()
            .WithTags("Admin")
            .WithSummary("List RAG search queries for a specific collection index"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        string indexName = Route<string>("indexName") ?? string.Empty;
        int page = Query<int?>("page") ?? 1;
        int pageSize = Query<int?>("pageSize") ?? 25;
        string? documentName = Query<string?>("documentName");

        if (string.IsNullOrWhiteSpace(indexName))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        // Normalize index name for comparison
        string normalizedIndex = indexName.ToLowerInvariant();

        Logger.LogInformation(
            "Fetching RAG search queries for index {IndexName}, page {Page}, pageSize {PageSize}, documentName {DocumentName}",
            normalizedIndex,
            page,
            pageSize,
            documentName ?? "(all)");

        await using JaimesDbContext dbContext = await dbContextFactory.CreateDbContextAsync(ct);

        // Build the base query - optionally filter by document name
        IQueryable<RagSearchQuery> baseQuery = dbContext.RagSearchQueries
            .AsNoTracking()
            .Include(q => q.ResultChunks)
            .Where(q => q.IndexName.ToLower() == normalizedIndex);

        // If filtering by document, only include queries that have results from that document
        if (!string.IsNullOrWhiteSpace(documentName))
        {
            baseQuery = baseQuery.Where(q => q.ResultChunks.Any(r => r.DocumentName == documentName));
        }

        // Get total count for pagination
        int totalCount = await baseQuery.CountAsync(ct);

        // Get paginated queries with their results
        List<RagSearchQuery> queries = await baseQuery
            .OrderByDescending(q => q.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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
            _ => $"{indexName} Collection"
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
                Page = page,
                PageSize = pageSize,
                Queries = queryInfos
            },
            ct);
    }
}
