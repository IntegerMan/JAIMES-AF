namespace MattEland.Jaimes.ApiService.Endpoints;

/// <summary>
/// Endpoint to get detailed information about a specific transcript message.
/// </summary>
public class GetTranscriptMessageDetailsEndpoint(IDbContextFactory<JaimesDbContext> dbContextFactory)
    : Ep.NoReq.Res<TranscriptMessageDetailsResponse>
{
    public override void Configure()
    {
        Get("/admin/transcript-messages/{messageId:int}");
        AllowAnonymous();
        Description(b => b
            .Produces<TranscriptMessageDetailsResponse>()
            .WithTags("Admin")
            .WithSummary("Get detailed information about a specific transcript message"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        int messageId = Route<int>("messageId");

        Logger.LogInformation("Fetching details for transcript message {MessageId}", messageId);

        await using JaimesDbContext dbContext = await dbContextFactory.CreateDbContextAsync(ct);

        // Get the message with its game
        Message? message = await dbContext.Messages
            .AsNoTracking()
            .Include(m => m.Game)
            .FirstOrDefaultAsync(m => m.Id == messageId, ct);

        if (message == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        // Get embedding if exists
        MessageEmbedding? embedding = await dbContext.MessageEmbeddings
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.MessageId == messageId, ct);

        // Build embedding preview and full embedding
        float[]? embeddingPreview = null;
        float[]? fullEmbedding = null;
        int embeddingDimensions = 0;
        if (embedding?.Embedding != null)
        {
            ReadOnlyMemory<float> memory = embedding.Embedding.ToArray();
            embeddingDimensions = memory.Length;
            embeddingPreview = memory.Span[..Math.Min(10, memory.Length)].ToArray();
            fullEmbedding = memory.ToArray();
        }

        // Get queries that returned this message
        // The ChunkId for transcript messages is formatted as the message ID
        string chunkIdToFind = messageId.ToString();
        List<RagSearchResultChunk> resultChunks = await dbContext.RagSearchResultChunks
            .AsNoTracking()
            .Include(r => r.RagSearchQuery)
            .Where(r => r.ChunkId == chunkIdToFind)
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

        Logger.LogInformation(
            "Returning details for message {MessageId} with {QueryCount} query appearances",
            messageId,
            queryAppearances.Length);

        await Send.OkAsync(new TranscriptMessageDetailsResponse
            {
                MessageId = message.Id,
                MessageText = message.Text,
                Role = message.PlayerId == null ? "assistant" : "user",
                HasEmbedding = embedding != null,
                QdrantPointId = embedding?.QdrantPointId,
                EmbeddingPreview = embeddingPreview,
                FullEmbedding = fullEmbedding,
                EmbeddingDimensions = embeddingDimensions,
                CreatedAt = message.CreatedAt,
                GameId = message.GameId,
                GameTitle = message.Game?.Title ?? "Unknown Game",
                QueryAppearances = queryAppearances
            },
            ct);
    }
}
