using MattEland.Jaimes.Domain;
using Microsoft.EntityFrameworkCore;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

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

        return new GameDto
        {
            GameId = game.Id,
            RulesetId = game.RulesetId,
            ScenarioId = game.ScenarioId,
            PlayerId = game.PlayerId,
            Messages = [new MessageDto(message.Text, null, "Game Master", message.CreatedAt)],
            ScenarioName = scenario.Name,
            RulesetName = (await context.Rulesets.FindAsync([rulesetId], cancellationToken))?.Name ?? rulesetId,
            PlayerName = player.Name,
            SystemPrompt = scenario.SystemPrompt
        };
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


        return new GameDto
        {
            GameId = game.Id,
            RulesetId = game.RulesetId,
            ScenarioId = game.ScenarioId,
            PlayerId = game.PlayerId,
            Messages = game.Messages
                .OrderBy(m => m.Id)
                .Select(m => new MessageDto(m.Text, m.PlayerId, m.Player?.Name ?? "Game Master", m.CreatedAt))
                .ToArray(),
            ScenarioName = game.Scenario?.Name ?? game.ScenarioId,
            RulesetName = game.Ruleset?.Name ?? game.RulesetId,
            PlayerName = game.Player?.Name ?? game.PlayerId,
            SystemPrompt = game.Scenario?.SystemPrompt ?? string.Empty
        };
    }

    public async Task<GameDto[]> GetGamesAsync(CancellationToken cancellationToken = default)
    {
        Game[] games = await context.Games
            .AsNoTracking()
            .Include(g => g.Scenario)
            .Include(g => g.Player)
            .Include(g => g.Ruleset)
            .ToArrayAsync(cancellationToken: cancellationToken);

        return games.Select(g => new GameDto
        {
            GameId = g.Id,
            PlayerId = g.PlayerId,
            RulesetId = g.RulesetId,
            ScenarioId = g.ScenarioId,
            Messages = [],
            ScenarioName = g.Scenario?.Name ?? g.ScenarioId,
            RulesetName = g.Ruleset?.Name ?? g.RulesetId,
            PlayerName = g.Player?.Name ?? g.PlayerId,
            SystemPrompt = g.Scenario?.SystemPrompt ?? string.Empty
        }).ToArray();
    }

    public async Task<ChatResponse> ProcessChatMessageAsync(Guid gameId, string message, CancellationToken cancellationToken = default)
    {
        // Get the game
        GameDto? gameDto = await GetGameAsync(gameId, cancellationToken);
        if (gameDto == null)
        {
            throw new ArgumentException($"Game '{gameId}' does not exist.", nameof(gameId));
        }

        // Get AI response from chat service
        ChatResponse chatResponse = await chatService.ProcessChatMessageAsync(gameDto, message, cancellationToken);

        // Create Message entities for persistence
        List<Message> messagesToPersist = [
            new() {
                GameId = gameDto.GameId,
                Text = message,
                PlayerId = gameDto.PlayerId,
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
