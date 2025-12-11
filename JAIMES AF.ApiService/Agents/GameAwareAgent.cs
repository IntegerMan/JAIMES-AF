using System.Text.Json;
using MattEland.Jaimes.Agents.Helpers;
using MattEland.Jaimes.Tools;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;

namespace MattEland.Jaimes.ApiService.Agents;

/// <summary>
/// A wrapper agent that creates game-specific agents based on the request route.
/// This allows us to use MapAGUI with game-specific agents.
/// </summary>
public class GameAwareAgent : AIAgent
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<GameAwareAgent> _logger;

    public GameAwareAgent(
        IServiceProvider serviceProvider,
        IHttpContextAccessor httpContextAccessor,
        ILogger<GameAwareAgent> logger)
    {
        _serviceProvider = serviceProvider;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public override async Task<AgentRunResponse> RunAsync(
        IEnumerable<ChatMessage> messages,
        AgentThread? thread = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        AIAgent gameAgent = await GetOrCreateGameAgentAsync(cancellationToken);
        AgentThread? gameThread = await GetOrCreateGameThreadAsync(gameAgent, cancellationToken);
        
        // Run with the game-specific agent and thread
        AgentRunResponse response = await gameAgent.RunAsync(messages, gameThread ?? thread, options, cancellationToken);
        
        // Persist messages and thread after completion
        await PersistGameStateAsync(response, cancellationToken);
        
        return response;
    }

    public override async IAsyncEnumerable<AgentRunResponseUpdate> RunStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentThread? thread = null,
        AgentRunOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        AIAgent gameAgent = await GetOrCreateGameAgentAsync(cancellationToken);
        AgentThread? gameThread = await GetOrCreateGameThreadAsync(gameAgent, cancellationToken);
        
        // Stream with the game-specific agent and thread
        await foreach (AgentRunResponseUpdate update in gameAgent.RunStreamingAsync(messages, gameThread ?? thread, options, cancellationToken))
        {
            yield return update;
        }
        
        // After streaming completes, get the final response to persist
        // Note: We'll need to run again to get the full response for persistence
        // This is a limitation - ideally we'd collect during streaming
        AgentRunResponse finalResponse = await gameAgent.RunAsync(messages, gameThread ?? thread, options, cancellationToken);
        await PersistGameStateAsync(finalResponse, cancellationToken);
    }

    private async Task<AIAgent> GetOrCreateGameAgentAsync(CancellationToken cancellationToken)
    {
        HttpContext? context = _httpContextAccessor.HttpContext;
        if (context == null)
        {
            throw new InvalidOperationException("HttpContext is not available");
        }

        // Extract gameId from route
        string? gameIdStr = context.Request.RouteValues["gameId"]?.ToString();
        if (!Guid.TryParse(gameIdStr, out Guid gameId))
        {
            throw new ArgumentException("Invalid game ID in route");
        }

        // Check if we already created the agent for this request
        string cacheKey = $"GameAgent_{gameId}";
        if (context.Items.TryGetValue(cacheKey, out object? cachedAgent) && cachedAgent is AIAgent agent)
        {
            return agent;
        }

        // Get scoped services
        using IServiceScope scope = _serviceProvider.CreateScope();
        IGameService gameService = scope.ServiceProvider.GetRequiredService<IGameService>();
        IChatClient chatClient = scope.ServiceProvider.GetRequiredService<IChatClient>();
        IRulesSearchService? rulesSearchService = scope.ServiceProvider.GetService<IRulesSearchService>();
        ILogger logger = scope.ServiceProvider.GetRequiredService<ILogger<GameAwareAgent>>();

        // Get the game
        GameDto? gameDto = await gameService.GetGameAsync(gameId, cancellationToken);
        if (gameDto == null)
        {
            throw new ArgumentException($"Game '{gameId}' does not exist.");
        }

        // Create agent with game context
        IChatClient instrumentedChatClient = chatClient.WrapWithInstrumentation(logger);
        AIAgent gameAgent = instrumentedChatClient.CreateJaimesAgent(
            logger,
            $"JaimesAgent-{gameId}",
            gameDto.Scenario.SystemPrompt,
            CreateTools(gameDto, rulesSearchService));

        // Cache it for this request
        context.Items[cacheKey] = gameAgent;
        context.Items["GameId"] = gameId;
        context.Items["GameDto"] = gameDto;

        return gameAgent;
    }

    private async Task<AgentThread?> GetOrCreateGameThreadAsync(AIAgent agent, CancellationToken cancellationToken)
    {
        HttpContext? context = _httpContextAccessor.HttpContext;
        if (context == null)
        {
            return null;
        }

        if (!context.Items.TryGetValue("GameId", out object? gameIdObj) || gameIdObj is not Guid gameId)
        {
            return null;
        }

        // Check if we already loaded the thread for this request
        string cacheKey = $"GameThread_{gameId}";
        if (context.Items.TryGetValue(cacheKey, out object? cachedThread) && cachedThread is AgentThread thread)
        {
            return thread;
        }

        // Get scoped service
        using IServiceScope scope = _serviceProvider.CreateScope();
        IChatHistoryService chatHistoryService = scope.ServiceProvider.GetRequiredService<IChatHistoryService>();

        // Load or create thread
        AgentThread? gameThread = null;
        string? existingThreadJson = await chatHistoryService.GetMostRecentThreadJsonAsync(gameId, cancellationToken);
        if (!string.IsNullOrEmpty(existingThreadJson))
        {
            JsonElement jsonElement = JsonSerializer.Deserialize<JsonElement>(existingThreadJson, JsonSerializerOptions.Web);
            gameThread = agent.DeserializeThread(jsonElement, JsonSerializerOptions.Web);
        }

        gameThread ??= agent.GetNewThread();

        // Cache it for this request
        context.Items[cacheKey] = gameThread;

        return gameThread;
    }

    private async Task PersistGameStateAsync(AgentRunResponse response, CancellationToken cancellationToken)
    {
        HttpContext? context = _httpContextAccessor.HttpContext;
        if (context == null || !context.Items.TryGetValue("GameId", out object? gameIdObj) || gameIdObj is not Guid gameId)
        {
            return;
        }

        if (!context.Items.TryGetValue("GameDto", out object? gameDtoObj) || gameDtoObj is not GameDto gameDto)
        {
            return;
        }

        if (!context.Items.TryGetValue($"GameThread_{gameId}", out object? threadObj) || threadObj is not AgentThread thread)
        {
            return;
        }

        // Extract the new user message for persistence
        ChatMessage? newUserMessage = response.Messages?.LastOrDefault(m => m.Role == ChatRole.User);
        if (newUserMessage == null)
        {
            return;
        }

        // Get scoped services
        using IServiceScope scope = _serviceProvider.CreateScope();
        IDbContextFactory<JaimesDbContext> contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<JaimesDbContext>>();
        IChatHistoryService chatHistoryService = scope.ServiceProvider.GetRequiredService<IChatHistoryService>();

        // Persist messages to database
        await using JaimesDbContext dbContext = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Save user message
        Message userMessageEntity = new()
        {
            GameId = gameId,
            Text = newUserMessage.Text ?? string.Empty,
            PlayerId = gameDto.Player.Id,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Messages.Add(userMessageEntity);

        // Save AI messages
        List<Message> aiMessageEntities = (response.Messages ?? [])
            .Where(m => m.Role == ChatRole.Assistant)
            .Select(m => new Message
            {
                GameId = gameId,
                Text = m.Text ?? string.Empty,
                PlayerId = null,
                CreatedAt = DateTime.UtcNow
            })
            .ToList();
        dbContext.Messages.AddRange(aiMessageEntities);

        await dbContext.SaveChangesAsync(cancellationToken);

        // Get the last AI message ID for thread association
        int? lastAiMessageId = aiMessageEntities.LastOrDefault()?.Id;

        // Serialize and save thread after completion
        string threadJson = thread.Serialize(JsonSerializerOptions.Web).GetRawText();
        await chatHistoryService.SaveThreadJsonAsync(gameId, threadJson, lastAiMessageId, cancellationToken);
    }

    private IList<AITool>? CreateTools(GameDto game, IRulesSearchService? rulesSearchService)
    {
        List<AITool> toolList = [];

        PlayerInfoTool playerInfoTool = new(game);
        AIFunction playerInfoFunction = AIFunctionFactory.Create(
            () => playerInfoTool.GetPlayerInfo(),
            "GetPlayerInfo",
            "Retrieves detailed information about the current player character in the game, including their name, unique identifier, and character description. Use this tool whenever you need to reference or describe the player character, their background, or their current state in the game world.");
        toolList.Add(playerInfoFunction);

        if (rulesSearchService != null)
        {
            RulesSearchTool rulesSearchTool = new(game, rulesSearchService);
            AIFunction rulesSearchFunction = AIFunctionFactory.Create(
                (string query) => rulesSearchTool.SearchRulesAsync(query),
                "SearchRules",
                "Searches the ruleset's indexed rules to find answers to specific questions or queries. This is a rules search tool that gets answers from rules to specific questions or queries. Use this tool whenever you need to look up game rules, mechanics, or rule clarifications. The tool will search through the indexed rules for the current scenario's ruleset and return relevant information.");
            toolList.Add(rulesSearchFunction);
        }

        return toolList;
    }

    public override AgentThread GetNewThread()
    {
        // Delegate to the game-specific agent if available, otherwise create a new thread
        // This is called by MapAGUI, but we'll handle thread creation in GetOrCreateGameThreadAsync
        // For now, return a basic thread - MapAGUI will handle it
        HttpContext? context = _httpContextAccessor.HttpContext;
        if (context != null && context.Items.TryGetValue($"GameThread_{context.Items["GameId"]}", out object? thread) && thread is AgentThread gameThread)
        {
            return gameThread;
        }
        
        // Fallback - create a basic thread
        // This shouldn't normally be called since we handle thread creation in GetOrCreateGameThreadAsync
        throw new InvalidOperationException("Cannot create thread without game context");
    }

    public override AgentThread DeserializeThread(JsonElement jsonElement, JsonSerializerOptions? options = null)
    {
        // Delegate to the game-specific agent if available
        HttpContext? context = _httpContextAccessor.HttpContext;
        if (context != null && context.Items.TryGetValue($"GameAgent_{context.Items["GameId"]}", out object? agent) && agent is AIAgent gameAgent)
        {
            return gameAgent.DeserializeThread(jsonElement, options);
        }
        
        // Fallback - this shouldn't normally be called
        throw new InvalidOperationException("Cannot deserialize thread without game context");
    }
}

