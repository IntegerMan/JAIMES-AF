using System.Collections.Generic;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.Extensions.AI;

namespace MattEland.Jaimes.ServiceLayer.Services;

public class GameService(
    IDbContextFactory<JaimesDbContext> contextFactory,
    IMessagePublisher messagePublisher) : IGameService
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

        // Save the game first
        context.Games.Add(game);
        await context.SaveChangesAsync(cancellationToken);

        // Use InitialGreeting if available, otherwise fall back to a generic greeting
        string greetingText = !string.IsNullOrWhiteSpace(scenario.InitialGreeting)
            ? scenario.InitialGreeting
            : "Welcome to the adventure!";

        // Find the scenario agent to attribute the system message to
        ScenarioAgent? scenarioAgent = await context.ScenarioAgents
            .AsNoTracking()
            .FirstOrDefaultAsync(sa => sa.ScenarioId == scenarioId, cancellationToken);

        if (scenarioAgent == null)
        {
            throw new InvalidOperationException(
                $"Scenario '{scenarioId}' does not have an associated agent. Cannot create game.");
        }

        // Create the initial message immediately (no AI call needed)
        // This message is displayed to the user and will be included as context
        // when the player sends their first message to the agent
        Message message = new()
        {
            GameId = game.Id,
            Text = greetingText,
            PlayerId = null, // System message, not from player
            CreatedAt = DateTime.UtcNow,
            IsScriptedMessage = true,
            AgentId = scenarioAgent.AgentId,
            InstructionVersionId = scenarioAgent.InstructionVersionId
        };

        context.Messages.Add(message);
        await context.SaveChangesAsync(cancellationToken);

        // Enqueue initial message for embedding
        ConversationMessageReadyForEmbeddingMessage embeddingMessage = new()
        {
            MessageId = message.Id,
            GameId = game.Id,
            Text = message.Text,
            Role = ChatRole.Assistant, // Initial greeting is from the system/assistant
            CreatedAt = message.CreatedAt
        };
        await messagePublisher.PublishAsync(embeddingMessage, cancellationToken);

        // Reload game with navigation properties for mapping
        Game gameWithNav = await context.Games
            .AsNoTracking()
            .Include(g => g.Messages)
            .ThenInclude(m => m.MessageSentiment)
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
            .ThenInclude(message => message.MessageSentiment)
            .Include(g => g.Messages)
            .ThenInclude(message => message.Player)
            .Include(g => g.Messages)
            .ThenInclude(message => message.InstructionVersion)
            .ThenInclude(iv => iv!.Model)
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

        // Query games without loading all messages (more efficient)
        Game[] games = await context.Games
            .AsNoTracking()
            .Include(g => g.Scenario)
            .Include(g => g.Player)
            .Include(g => g.Ruleset)
            .ToArrayAsync(cancellationToken);

        // Query max CreatedAt per game directly from Messages table (efficient database query)
        Dictionary<Guid, DateTime> lastPlayedAtByGameId = await context.Messages
            .AsNoTracking()
            .GroupBy(m => m.GameId)
            .Select(g => new {GameId = g.Key, LastPlayedAt = g.Max(m => m.CreatedAt)})
            .ToDictionaryAsync(x => x.GameId, x => x.LastPlayedAt, cancellationToken);

        // Map games to DTOs, using the pre-calculated LastPlayedAt values
        return games.Select(game =>
            {
                DateTime? lastPlayedAt = lastPlayedAtByGameId.TryGetValue(game.Id, out DateTime value)
                    ? value
                    : null;
                return game.ToDto(lastPlayedAt);
            })
            .ToArray();
    }


    public async Task<GameDto?> UpdateGameAsync(Guid gameId,
        string? title,
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        Game? game = await context.Games
            .Include(g => g.Scenario)
            .Include(g => g.Player)
            .Include(g => g.Ruleset)
            .FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken);

        if (game == null) return null;

        game.Title = title;
        await context.SaveChangesAsync(cancellationToken);

        // Calculate lastPlayedAt for the DTO
        DateTime? lastPlayedAt = await context.Messages
            .Where(m => m.GameId == gameId)
            .MaxAsync(m => (DateTime?) m.CreatedAt, cancellationToken);

        return game.ToDto(lastPlayedAt);
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
