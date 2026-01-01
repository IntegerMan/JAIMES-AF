using MattEland.Jaimes.Agents.Helpers;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.Tools;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MattEland.Jaimes.ApiService.Endpoints;

/// <summary>
/// Factory that creates game-specific agents for AG-UI endpoints.
/// </summary>
public class GameAwareAgentFactory
{
    private readonly IGameService _gameService;
    private readonly IChatClient _chatClient;
    private readonly ILogger<GameAwareAgentFactory> _logger;
    private readonly IChatHistoryService _chatHistoryService;
    private readonly IServiceProvider _serviceProvider;

    public GameAwareAgentFactory(
        IGameService gameService,
        IChatClient chatClient,
        IChatHistoryService chatHistoryService,
        ILogger<GameAwareAgentFactory> logger,
        IServiceProvider serviceProvider)
    {
        _gameService = gameService;
        _chatClient = chatClient;
        _logger = logger;
        _chatHistoryService = chatHistoryService;
        _serviceProvider = serviceProvider;
    }

    public async Task<AIAgent> CreateAgentForGameAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        MattEland.Jaimes.Domain.GameDto? gameDto = await _gameService.GetGameAsync(gameId, cancellationToken);
        if (gameDto == null)
        {
            throw new ArgumentException($"Game '{gameId}' does not exist.", nameof(gameId));
        }

        // Get instructions from InstructionService (combines base agent instructions with scenario instructions)
        // This ensures consistency with GameAwareAgent.GetOrCreateGameAgentAsync
        using IServiceScope scope = _serviceProvider.CreateScope();
        IInstructionService instructionService = scope.ServiceProvider.GetRequiredService<IInstructionService>();

        string? systemPrompt;
        if (!string.IsNullOrEmpty(gameDto.AgentId) && gameDto.InstructionVersionId.HasValue)
        {
            // Use specific agent version if overridden in the game
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<JaimesDbContext>>();
            await using var dbContext = await contextFactory.CreateDbContextAsync(cancellationToken);

            var agentVersion = await dbContext.AgentInstructionVersions
                .AsNoTracking()
                .FirstOrDefaultAsync(av => av.AgentId == gameDto.AgentId && av.Id == gameDto.InstructionVersionId.Value,
                    cancellationToken);

            systemPrompt = agentVersion?.Instructions;

            // Still include scenario instructions if any
            if (!string.IsNullOrWhiteSpace(gameDto.Scenario.ScenarioInstructions))
            {
                systemPrompt = $"{systemPrompt}\n\n---\n\n{gameDto.Scenario.ScenarioInstructions}";
            }
        }
        else
        {
            systemPrompt = await instructionService.GetInstructionsAsync(gameDto.Scenario.Id, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(systemPrompt))
        {
            _logger.LogWarning("Game {GameId} has no instructions configured, using default", gameId);
            systemPrompt = "You are a helpful game master assistant.";
        }

        // Create the agent using the same Jaimes-specific helper as GameAwareAgent
        return _chatClient.CreateJaimesAgent(
            _logger,
            gameDto.Title ?? "Game Master",
            systemPrompt,
            null, // No tools needed for basic factory creation for AGUI consumption in this context
            () => _serviceProvider);
    }
}
