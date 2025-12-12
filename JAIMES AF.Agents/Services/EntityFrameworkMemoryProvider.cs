namespace MattEland.Jaimes.Agents.Services;

/// <summary>
/// Entity Framework implementation of the MemoryProvider pattern for Agent Framework.
/// This provider integrates with PostgreSQL through Entity Framework Core to persist
/// conversation history and agent thread state.
/// </summary>
public class EntityFrameworkMemoryProvider : IMemoryProvider
{
    private readonly IDbContextFactory<JaimesDbContext> _contextFactory;
    private readonly ILogger<EntityFrameworkMemoryProvider> _logger;

    public EntityFrameworkMemoryProvider(
        IDbContextFactory<JaimesDbContext> contextFactory,
        ILogger<EntityFrameworkMemoryProvider> logger)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<AgentThread> LoadThreadAsync(
        Guid gameId,
        AIAgent agent,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Loading thread for game {GameId}", gameId);

        await using JaimesDbContext context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        Game? game = await context.Games
            .Include(g => g.MostRecentHistory)
            .FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken);

        if (game?.MostRecentHistory?.ThreadJson != null)
        {
            _logger.LogInformation(
                "Loaded existing thread for game {GameId} with {Length} characters",
                gameId,
                game.MostRecentHistory.ThreadJson.Length);

            try
            {
                JsonElement jsonElement = JsonSerializer.Deserialize<JsonElement>(
                    game.MostRecentHistory.ThreadJson,
                    JsonSerializerOptions.Web);

                return agent.DeserializeThread(jsonElement, JsonSerializerOptions.Web);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize thread for game {GameId}, creating new thread", gameId);
            }
        }

        _logger.LogInformation("No existing thread found for game {GameId}, creating new thread", gameId);
        return agent.GetNewThread();
    }

    /// <inheritdoc />
    public async Task SaveConversationAsync(
        Guid gameId,
        string playerId,
        ChatMessage? userMessage,
        IEnumerable<ChatMessage> assistantMessages,
        AgentThread thread,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Saving conversation for game {GameId}", gameId);

        await using JaimesDbContext context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        // Save user message if present
        if (userMessage != null && !string.IsNullOrEmpty(userMessage.Text))
        {
            Message userMessageEntity = new()
            {
                GameId = gameId,
                Text = userMessage.Text,
                PlayerId = playerId,
                CreatedAt = DateTime.UtcNow
            };
            context.Messages.Add(userMessageEntity);
            _logger.LogDebug("Added user message for game {GameId}", gameId);
        }

        // Save assistant messages
        List<Message> aiMessageEntities = assistantMessages
            .Where(m => !string.IsNullOrEmpty(m.Text))
            .Select(m => new Message
            {
                GameId = gameId,
                Text = m.Text ?? string.Empty,
                PlayerId = null, // null indicates Game Master
                CreatedAt = DateTime.UtcNow
            })
            .ToList();

        if (aiMessageEntities.Count > 0)
        {
            context.Messages.AddRange(aiMessageEntities);
            _logger.LogDebug("Added {Count} assistant message(s) for game {GameId}", aiMessageEntities.Count, gameId);
        }

        await context.SaveChangesAsync(cancellationToken);

        // Get the last AI message ID for thread association
        int? lastAiMessageId = aiMessageEntities.LastOrDefault()?.Id;

        // Serialize and save thread state
        string threadJson = thread.Serialize(JsonSerializerOptions.Web).GetRawText();
        await SaveThreadJsonAsync(context, gameId, threadJson, lastAiMessageId, cancellationToken);

        _logger.LogInformation(
            "Saved conversation for game {GameId}: {MessageCount} message(s) and thread state",
            gameId,
            (userMessage != null ? 1 : 0) + aiMessageEntities.Count);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ChatMessage>> GetConversationHistoryAsync(
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Loading conversation history for game {GameId}", gameId);

        await using JaimesDbContext context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        List<Message> messages = await context.Messages
            .Where(m => m.GameId == gameId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        List<ChatMessage> chatMessages = messages
            .Select(m => new ChatMessage(
                m.PlayerId == null ? ChatRole.Assistant : ChatRole.User,
                m.Text)
            {
                AuthorName = m.PlayerId
            })
            .ToList();

        _logger.LogInformation(
            "Loaded {Count} message(s) from conversation history for game {GameId}",
            chatMessages.Count,
            gameId);

        return chatMessages;
    }

    private async Task SaveThreadJsonAsync(
        JaimesDbContext context,
        Guid gameId,
        string threadJson,
        int? messageId,
        CancellationToken cancellationToken)
    {
        Game? game = await context.Games
            .Include(g => g.MostRecentHistory)
            .FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken);

        if (game == null)
        {
            throw new ArgumentException($"Game '{gameId}' does not exist.", nameof(gameId));
        }

        ChatHistory newHistory = new()
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            ThreadJson = threadJson,
            CreatedAt = DateTime.UtcNow,
            PreviousHistoryId = game.MostRecentHistory?.Id,
            MessageId = messageId
        };

        context.ChatHistories.Add(newHistory);
        game.MostRecentHistoryId = newHistory.Id;
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "Saved thread JSON for game {GameId}, history ID: {HistoryId}",
            gameId,
            newHistory.Id);
    }
}
