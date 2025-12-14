using System.Reflection;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MattEland.Jaimes.Agents.Helpers;

namespace MattEland.Jaimes.ServiceLayer.Services;

public class GameService(
    IDbContextFactory<JaimesDbContext> contextFactory,
    IChatHistoryService chatHistoryService,
    IChatClient chatClient,
    ILoggerFactory loggerFactory) : IGameService
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

        // Use InitialGreeting if available, otherwise fall back to a generic greeting
        string greetingText = !string.IsNullOrWhiteSpace(scenario.InitialGreeting)
            ? scenario.InitialGreeting
            : "Welcome to the adventure!";

        // Create the initial message immediately (no AI call needed)
        Message message = new()
        {
            GameId = game.Id,
            Text = greetingText,
            PlayerId = null, // System message, not from player
            CreatedAt = DateTime.UtcNow
        };

        context.Messages.Add(message);
        await context.SaveChangesAsync(cancellationToken);

        // Create an agent thread for the game
        // The greeting message is already saved in the database, so we don't need to call the LLM
        // The thread will be populated with conversation history as messages are sent
        ILogger logger = loggerFactory.CreateLogger<GameService>();
        IChatClient instrumentedChatClient = chatClient.WrapWithInstrumentation(logger);

        // Create a minimal agent just for thread creation
        // We use the scenario's system prompt to ensure consistency
        string systemPrompt = !string.IsNullOrWhiteSpace(scenario.SystemPrompt)
            ? scenario.SystemPrompt
            : "You are a helpful game master assistant.";

        AIAgent agent = instrumentedChatClient.CreateJaimesAgent(logger, $"JaimesAgent-{game.Id}", systemPrompt, null);
        AgentThread thread = agent.GetNewThread();

        // Add the initial greeting message to the thread
        // This ensures the AI is aware of the greeting when the game is loaded
        ChatMessage greetingChatMessage = new(ChatRole.Assistant, greetingText);

        // Use reflection to call the protected static NotifyThreadOfNewMessagesAsync method
        // This notifies the thread about the initial greeting message
        MethodInfo? notifyMethod = typeof(AIAgent).GetMethod(
            "NotifyThreadOfNewMessagesAsync",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            [typeof(AgentThread), typeof(IEnumerable<ChatMessage>), typeof(CancellationToken)],
            null);

        if (notifyMethod != null)
        {
            try
            {
                await (Task)notifyMethod.Invoke(null, [thread, new[] { greetingChatMessage }, cancellationToken])!;
                logger.LogDebug("Successfully added initial greeting message to thread via reflection");
            }
            catch (Exception ex)
            {
                // If the reflection call fails, we cannot safely proceed as the greeting won't be in the thread
                // This would cause inconsistent state: greeting in Messages table but not in thread JSON
                logger.LogError(ex, "Failed to add initial greeting message to thread via reflection. This would cause inconsistent state.");
                throw new InvalidOperationException(
                    "Failed to add initial greeting message to agent thread. The greeting message exists in the database but cannot be added to the thread. " +
                    "This would cause inconsistent state where the AI won't have the greeting in its conversation context. " +
                    "Please ensure the Microsoft.Agents.AI library version supports NotifyThreadOfNewMessagesAsync.",
                    ex);
            }
        }
        else
        {
            // If the method doesn't exist, we cannot safely proceed as the greeting won't be in the thread
            // This would cause inconsistent state: greeting in Messages table but not in thread JSON
            logger.LogError("Could not find NotifyThreadOfNewMessagesAsync method via reflection. Cannot add greeting to thread.");
            throw new InvalidOperationException(
                "Cannot add initial greeting message to agent thread: NotifyThreadOfNewMessagesAsync method not found. " +
                "The greeting message exists in the database but cannot be added to the thread. " +
                "This would cause inconsistent state where the AI won't have the greeting in its conversation context. " +
                "Please ensure the Microsoft.Agents.AI library version supports NotifyThreadOfNewMessagesAsync.");
        }

        // Serialize the thread with the greeting message and save it
        string threadJson = thread.Serialize(JsonSerializerOptions.Web).GetRawText();
        await chatHistoryService.SaveThreadJsonAsync(game.Id, threadJson, message.Id, cancellationToken);

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