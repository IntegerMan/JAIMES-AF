using MattEland.Jaimes.Agents.Helpers;
using MattEland.Jaimes.Tools;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;

namespace MattEland.Jaimes.ApiService.Endpoints;

/// <summary>
/// Factory that creates game-specific agents for AG-UI endpoints.
/// </summary>
public class GameAwareAgentFactory
{
    private readonly IGameService _gameService;
    private readonly IChatClient _chatClient;
    private readonly ILogger<GameAwareAgentFactory> _logger;
    private readonly IRulesSearchService? _rulesSearchService;
    private readonly IChatHistoryService _chatHistoryService;

    public GameAwareAgentFactory(
        IGameService gameService,
        IChatClient chatClient,
        ILogger<GameAwareAgentFactory> logger,
        IChatHistoryService chatHistoryService,
        IRulesSearchService? rulesSearchService = null)
    {
        _gameService = gameService;
        _chatClient = chatClient;
        _logger = logger;
        _chatHistoryService = chatHistoryService;
        _rulesSearchService = rulesSearchService;
    }

    public async Task<AIAgent> CreateAgentForGameAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        GameDto? gameDto = await _gameService.GetGameAsync(gameId, cancellationToken);
        if (gameDto == null)
        {
            throw new ArgumentException($"Game '{gameId}' does not exist.", nameof(gameId));
        }

        IChatClient instrumentedChatClient = _chatClient.WrapWithInstrumentation(_logger);
        return instrumentedChatClient.CreateJaimesAgent(
            _logger,
            $"JaimesAgent-{gameId}",
            gameDto.Scenario.SystemPrompt,
            CreateTools(gameDto));
    }

    public async Task<AgentThread?> GetOrCreateThreadAsync(Guid gameId, AIAgent agent, CancellationToken cancellationToken = default)
    {
        string? existingThreadJson = await _chatHistoryService.GetMostRecentThreadJsonAsync(gameId, cancellationToken);
        if (!string.IsNullOrEmpty(existingThreadJson))
        {
            System.Text.Json.JsonElement jsonElement = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(existingThreadJson, System.Text.Json.JsonSerializerOptions.Web);
            return agent.DeserializeThread(jsonElement, System.Text.Json.JsonSerializerOptions.Web);
        }

        return agent.GetNewThread();
    }

    private IList<AITool>? CreateTools(GameDto game)
    {
        List<AITool> toolList = [];

        PlayerInfoTool playerInfoTool = new(game);
        AIFunction playerInfoFunction = AIFunctionFactory.Create(
            () => playerInfoTool.GetPlayerInfo(),
            "GetPlayerInfo",
            "Retrieves detailed information about the current player character in the game, including their name, unique identifier, and character description. Use this tool whenever you need to reference or describe the player character, their background, or their current state in the game world.");
        toolList.Add(playerInfoFunction);

        if (_rulesSearchService != null)
        {
            RulesSearchTool rulesSearchTool = new(game, _rulesSearchService);
            AIFunction rulesSearchFunction = AIFunctionFactory.Create(
                (string query) => rulesSearchTool.SearchRulesAsync(query),
                "SearchRules",
                "Searches the ruleset's indexed rules to find answers to specific questions or queries. This is a rules search tool that gets answers from rules to specific questions or queries. Use this tool whenever you need to look up game rules, mechanics, or rule clarifications. The tool will search through the indexed rules for the current scenario's ruleset and return relevant information.");
            toolList.Add(rulesSearchFunction);
        }

        return toolList;
    }
}

