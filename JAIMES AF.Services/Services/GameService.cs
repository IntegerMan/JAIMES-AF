using MattEland.Jaimes.Domain;
using Microsoft.EntityFrameworkCore;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.ServiceLayer.Mapping;

namespace MattEland.Jaimes.ServiceLayer.Services;

public class GameService(JaimesDbContext context, IChatService chatService, IChatHistoryService chatHistoryService) : IGameService
{
    public async Task<GameDto> CreateGameAsync(string scenarioId, string playerId, CancellationToken cancellationToken = default)
    {
        // Validate that the player exists
        var player = await context.Players.FindAsync([playerId], cancellationToken);
        if (player == null)
        {
            throw new ArgumentException($"Player '{playerId}' does not exist.", nameof(playerId));
        }

        // Validate that the scenario exists
        var scenario = await context.Scenarios.FindAsync([scenarioId], cancellationToken);
        if (scenario == null)
        {
            throw new ArgumentException($"Scenario '{scenarioId}' does not exist.", nameof(scenarioId));
        }

        // Validate that player and scenario have the same ruleset
        if (player.RulesetId != scenario.RulesetId)
        {
            throw new ArgumentException($"Player '{playerId}' uses ruleset '{player.RulesetId}' but scenario '{scenarioId}' uses ruleset '{scenario.RulesetId}'. They must use the same ruleset.", nameof(scenarioId));
        }

        // Use the ruleset from the player (which we've validated matches the scenario)
        string rulesetId = player.RulesetId;

        Game game = new Game
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

        InitialMessageResponse initialResponse = await chatService.GenerateInitialMessageAsync(request, cancellationToken);

        // Create the initial message from the AI
        Message message = new Message
        {
            GameId = game.Id,
            Text = initialResponse.Message,
            PlayerId = null, // AI-generated message, not from player
            CreatedAt = DateTime.UtcNow
        };

        context.Messages.Add(message);
        await context.SaveChangesAsync(cancellationToken);

        // Save the thread JSON with the message ID
        await chatHistoryService.SaveThreadJsonAsync(game.Id, initialResponse.ThreadJson, message.Id, cancellationToken);

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
        Game? game = await context.Games
            .AsNoTracking()
            .Include(g => g.Messages).ThenInclude(message => message.Player)
            .Include(g => g.Scenario)
            .Include(g => g.Player)
            .Include(g => g.Ruleset)
            .FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken);

        if (game == null)
        {
            return null;
        }

        return game.ToDto();
    }

    public async Task<GameDto[]> GetGamesAsync(CancellationToken cancellationToken = default)
    {
        Game[] games = await context.Games
            .AsNoTracking()
            .Include(g => g.Scenario)
            .Include(g => g.Player)
            .Include(g => g.Ruleset)
            .ToArrayAsync(cancellationToken: cancellationToken);

        return games.ToDto();
    }

    public async Task<JaimesChatResponse> ProcessChatMessageAsync(Guid gameId, string message, CancellationToken cancellationToken = default)
    {
        // Get the game
        GameDto? gameDto = await GetGameAsync(gameId, cancellationToken);
        if (gameDto == null)
        {
            throw new ArgumentException($"Game '{gameId}' does not exist.", nameof(gameId));
        }

        // Get AI response from chat service
        JaimesChatResponse chatResponse = await chatService.ProcessChatMessageAsync(gameDto, message, cancellationToken);

        // Create Message entities for persistence
        List<Message> messagesToPersist = [
            new() {
                GameId = gameDto.GameId,
                Text = message,
                PlayerId = gameDto.Player.Id,
                CreatedAt = DateTime.UtcNow
            }
        ];
        messagesToPersist.AddRange(chatResponse.Messages.Select(m => new Message
        {
            GameId = gameDto.GameId,
            Text = m.Text,
            PlayerId = null,
            CreatedAt = m.CreatedAt
        }));

        // Persist messages
        await context.Messages.AddRangeAsync(messagesToPersist, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        // Get the last AI message ID (last message where PlayerId == null)
        // After SaveChangesAsync, EF Core will have populated the Id property
        int? lastAiMessageId = messagesToPersist
            .Where(m => m.PlayerId == null)
            .LastOrDefault()?.Id;

        // Save the thread JSON if provided
        if (!string.IsNullOrEmpty(chatResponse.ThreadJson))
        {
            await chatHistoryService.SaveThreadJsonAsync(gameDto.GameId, chatResponse.ThreadJson, lastAiMessageId, cancellationToken);
        }

        return chatResponse;
    }

    public async Task DeleteGameAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        Game? game = await context.Games.FindAsync([gameId], cancellationToken);
        if (game == null)
        {
            throw new ArgumentException($"Game '{gameId}' does not exist.", nameof(gameId));
        }

        context.Games.Remove(game);
        await context.SaveChangesAsync(cancellationToken);
    }
}
