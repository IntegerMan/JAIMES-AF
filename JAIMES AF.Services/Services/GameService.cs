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
        string? title,
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

        // Validate that the scenario has an associated agent BEFORE creating the game
        // This prevents orphaned game records if the scenario is misconfigured
        ScenarioAgent? scenarioAgent = await context.ScenarioAgents
            .AsNoTracking()
            .FirstOrDefaultAsync(sa => sa.ScenarioId == scenarioId, cancellationToken);

        if (scenarioAgent == null)
        {
            throw new InvalidOperationException(
                $"Scenario '{scenarioId}' does not have an associated agent. Cannot create game.");
        }

        // Use the ruleset from the player (which we've validated matches the scenario)
        string rulesetId = player.RulesetId;

        Game game = new()
        {
            Id = Guid.NewGuid(),
            RulesetId = rulesetId,
            ScenarioId = scenarioId,
            PlayerId = playerId,
            Title = title ?? "Untitled Game",
            CreatedAt = DateTime.UtcNow,
            AgentId = scenarioAgent.AgentId,
            InstructionVersionId = scenarioAgent.InstructionVersionId
        };

        // Determine the specific version ID for the initial message
        // If the game is dynamic (null), we must resolve the specific "Latest" version now for the message record
        int initialMessageVersionId;
        if (scenarioAgent.InstructionVersionId.HasValue)
        {
            initialMessageVersionId = scenarioAgent.InstructionVersionId.Value;
        }
        else
        {
            initialMessageVersionId = await context.AgentInstructionVersions
                .AsNoTracking()
                .Where(v => v.AgentId == scenarioAgent.AgentId && v.IsActive)
                .OrderByDescending(v => v.CreatedAt)
                .Select(v => v.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (initialMessageVersionId == 0)
            {
                throw new InvalidOperationException(
                    $"No active instruction version found for agent '{scenarioAgent.AgentId}'. Cannot create game.");
            }
        }

        // Save the game first
        context.Games.Add(game);
        await context.SaveChangesAsync(cancellationToken);

        // Use InitialGreeting if available, otherwise fall back to a generic greeting
        string greetingText = !string.IsNullOrWhiteSpace(scenario.InitialGreeting)
            ? scenario.InitialGreeting
            : "Welcome to the adventure!";

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
            InstructionVersionId = initialMessageVersionId
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
            .Include(g => g.Messages)
            .ThenInclude(m => m.Agent)
            .Include(g => g.Messages)
            .ThenInclude(m => m.InstructionVersion)
            .ThenInclude(iv => iv!.Model)
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
            .ThenInclude(message => message.Agent)
            .Include(g => g.Messages)
            .ThenInclude(message => message.Player)
            .Include(g => g.Messages)
            .ThenInclude(message => message.InstructionVersion)
            .ThenInclude(iv => iv!.Model)
            .Include(g => g.Scenario)
            .Include(g => g.Player)
            .Include(g => g.Agent)
            .Include(g => g.InstructionVersion)
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
            .ThenInclude(s => s!.ScenarioAgents)
            .ThenInclude(sa => sa!.Agent)
            .Include(g => g.Scenario)
            .ThenInclude(s => s!.ScenarioAgents)
            .ThenInclude(sa => sa!.InstructionVersion)
            .Include(g => g.Player)
            .Include(g => g.Ruleset)
            .Include(g => g.Agent)
            .Include(g => g.InstructionVersion)
            .ToArrayAsync(cancellationToken);

        // Query max CreatedAt per game directly from Messages table (efficient database query)
        Dictionary<Guid, DateTime> lastPlayedAtByGameId = await context.Messages
            .AsNoTracking()
            .GroupBy(m => m.GameId)
            .Select(g => new { GameId = g.Key, LastPlayedAt = g.Max(m => m.CreatedAt) })
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
        string? agentId = null,
        int? instructionVersionId = null,
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        Game? game = await context.Games
            .Include(g => g.Scenario)
            .Include(g => g.Player)
            .Include(g => g.Ruleset)
            .FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken);

        if (game == null) return null;

        if (title != null) game.Title = title;

        // Determine effective agent ID for validation
        string? effectiveAgentId = agentId ?? game.AgentId;

        if (effectiveAgentId == null)
        {
            // Fallback to scenario agent if no override is present
            effectiveAgentId = await context.ScenarioAgents
                .Where(sa => sa.ScenarioId == game.ScenarioId)
                .Select(sa => sa.AgentId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (instructionVersionId.HasValue)
        {
            // If we STILL don't have an agent (shouldn't happen), we can't validate version ownership
            if (string.IsNullOrEmpty(effectiveAgentId))
            {
                throw new InvalidOperationException(
                    "Cannot validate instruction version because no agent is associated with this game or scenario.");
            }

            // Validate that the version belongs to the effective agent
            bool versionExistsForAgent = await context.AgentInstructionVersions
                .AnyAsync(v => v.Id == instructionVersionId.Value && v.AgentId == effectiveAgentId, cancellationToken);

            if (!versionExistsForAgent)
            {
                throw new ArgumentException(
                    $"Instruction version '{instructionVersionId.Value}' does not belong to agent '{effectiveAgentId}'.",
                    nameof(instructionVersionId));
            }
        }

        // If agentId is provided, we treat it as a reconfiguration of the game's agent
        // allowing setting InstructionVersionId to null (meaning "Use Latest")
        if (agentId != null)
        {
            game.AgentId = agentId;
            game.InstructionVersionId = instructionVersionId;
        }
        else if (instructionVersionId.HasValue)
        {
            game.InstructionVersionId = instructionVersionId;
        }

        await context.SaveChangesAsync(cancellationToken);

        // Calculate lastPlayedAt for the DTO
        DateTime? lastPlayedAt = await context.Messages
            .Where(m => m.GameId == gameId)
            .MaxAsync(m => (DateTime?)m.CreatedAt, cancellationToken);

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
