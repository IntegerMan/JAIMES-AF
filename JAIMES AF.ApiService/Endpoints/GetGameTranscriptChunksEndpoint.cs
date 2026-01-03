namespace MattEland.Jaimes.ApiService.Endpoints;

/// <summary>
/// Endpoint to list transcript messages (as chunks) for a specific game.
/// </summary>
public class GetGameTranscriptChunksEndpoint(IDbContextFactory<JaimesDbContext> dbContextFactory)
    : Ep.NoReq.Res<TranscriptChunksResponse>
{
    public override void Configure()
    {
        Get("/admin/games/{gameId:guid}/transcript-chunks");
        AllowAnonymous();
        Description(b => b
            .Produces<TranscriptChunksResponse>()
            .WithTags("Admin")
            .WithSummary("List transcript messages for a specific game with pagination"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        Guid gameId = Route<Guid>("gameId");
        int page = Query<int?>("page") ?? 1;
        int pageSize = Query<int?>("pageSize") ?? 25;

        Logger.LogInformation(
            "Fetching transcript chunks for game {GameId}, page {Page}, pageSize {PageSize}",
            gameId,
            page,
            pageSize);

        await using JaimesDbContext dbContext = await dbContextFactory.CreateDbContextAsync(ct);

        // Get the game
        Game? game = await dbContext.Games
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == gameId, ct);

        if (game == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        // Get total count
        int totalCount = await dbContext.Messages
            .AsNoTracking()
            .CountAsync(m => m.GameId == gameId, ct);

        // Get message IDs that have embeddings
        HashSet<int> embeddedMessageIds = (await dbContext.MessageEmbeddings
                .AsNoTracking()
                .Include(e => e.Message)
                .Where(e => e.Message!.GameId == gameId)
                .Select(e => e.MessageId)
                .ToListAsync(ct))
            .ToHashSet();

        // Get paginated messages
        List<Message> messages = await dbContext.Messages
            .AsNoTracking()
            .Where(m => m.GameId == gameId)
            .OrderBy(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // Map to response with index
        int startIndex = (page - 1) * pageSize;
        TranscriptChunkInfo[] chunkInfos = messages.Select((m, idx) => new TranscriptChunkInfo
            {
                MessageId = m.Id,
                MessageIndex = startIndex + idx,
                MessageTextPreview = m.Text.Length > 200
                    ? m.Text[..200] + "..."
                    : m.Text,
                HasEmbedding = embeddedMessageIds.Contains(m.Id),
                Role = m.PlayerId == null ? "assistant" : "user",
                CreatedAt = m.CreatedAt
            })
            .ToArray();

        Logger.LogInformation(
            "Returning {MessageCount} messages out of {TotalCount} for game {GameId}",
            chunkInfos.Length,
            totalCount,
            gameId);

        await Send.OkAsync(new TranscriptChunksResponse
            {
                GameId = gameId,
                GameTitle = game.Title,
                Chunks = chunkInfos,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            },
            ct);
    }
}
