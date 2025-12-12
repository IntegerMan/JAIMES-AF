namespace MattEland.Jaimes.ServiceLayer.Services;

public class GameService(
    IDbContextFactory<JaimesDbContext> contextFactory,
    IChatService chatService,
    IChatHistoryService chatHistoryService) : IGameService
{
    public async Task<GameDto> CreateGameAsync(string scenarioId,
        string playerId,
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Validate that the player exists
        Player? player = await context.Players.FindAsync([playerId], cancellationToken);
        if (player == null) throw new ArgumentException($"Player '{playerId}' does not exist.", nameof(playerId));

        // Validate that the scenario exists
        Scenario? scenario = await context.Scenarios.FindAsync([scenarioId], cancellationToken);
        if (scenario == null)
            throw new ArgumentException($"Scenario '{scenarioId}' does not exist.", nameof(scenarioId));

        // Validate that player and scenario have the same ruleset
        if (player.RulesetId != scenario.RulesetId)
            throw new ArgumentException(
                $"Player '{playerId}' uses ruleset '{player.RulesetId}' but scenario '{scenarioId}' uses ruleset '{scenario.RulesetId}'. They must use the same ruleset.",
                nameof(scenarioId));

        // Use the ruleset from the player (which we've validated matches the scenario)
        string rulesetId = player.RulesetId;

        Game game = new()
        {
            Id = Guid.NewGuid(),
            RulesetId = rulesetId,
            ScenarioId = scenarioId,
            PlayerId = playerId,
            CreatedAt = DateTime.UtcNow
        };

        // Save the game first so it exists for the chat service
        context.Games.Add(game);
        await context.SaveChangesAsync(cancellationToken);

        // Generate the initial message using the chat service
        GenerateInitialMessageRequest request = new()
        {
            GameId = game.Id,
            SystemPrompt = scenario.SystemPrompt,
            NewGameInstructions = scenario.NewGameInstructions,
            PlayerName = player.Name,
            PlayerDescription = player.Description
        };

        InitialMessageResponse initialResponse =
            await chatService.GenerateInitialMessageAsync(request, cancellationToken);

        // Create the initial message from the AI
        Message message = new()
        {
            GameId = game.Id,
            Text = initialResponse.Message,
            PlayerId = null, // AI-generated message, not from player
            CreatedAt = DateTime.UtcNow
        };

        context.Messages.Add(message);
        await context.SaveChangesAsync(cancellationToken);

        // Save the thread JSON with the message ID
        await chatHistoryService.SaveThreadJsonAsync(game.Id,
            initialResponse.ThreadJson,
            message.Id,
            cancellationToken);

        // Reload game with navigation properties for mapping
        Game gameWithNav = await context.Games
            .AsNoTracking()
            .Include(g => g.Messages)
            .Include(g => g.Scenario)
            .Include(g => g.Player)
            .Include(g => g.Ruleset)
            .FirstAsync(g => g.Id == game.Id, cancellationToken);

        return gameWithNav.ToDto();
    }

    public async Task<GameDto?> GetGameAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        Game? game = await context.Games
            .AsNoTracking()
            .Include(g => g.Messages)
            .ThenInclude(message => message.Player)
            .Include(g => g.Scenario)
            .Include(g => g.Player)
            .Include(g => g.Ruleset)
            .FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken);

        if (game == null) return null;

        return game.ToDto();
    }

    public async Task<GameDto[]> GetGamesAsync(CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        Game[] games = await context.Games
            .AsNoTracking()
            .Include(g => g.Scenario)
            .Include(g => g.Player)
            .Include(g => g.Ruleset)
            .ToArrayAsync(cancellationToken);

        return games.ToDto();
    }


    public async Task DeleteGameAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        Game? game = await context.Games.FindAsync([gameId], cancellationToken);
        if (game == null) throw new ArgumentException($"Game '{gameId}' does not exist.", nameof(gameId));

        context.Games.Remove(game);
        await context.SaveChangesAsync(cancellationToken);
    }
}