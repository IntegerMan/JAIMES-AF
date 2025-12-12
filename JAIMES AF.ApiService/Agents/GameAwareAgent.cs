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
        HttpContext? context = _httpContextAccessor.HttpContext;
        string? gameIdStr = context?.Request.RouteValues["gameId"]?.ToString();
        Guid.TryParse(gameIdStr, out Guid gameId);
        
        _logger.LogInformation("GameAwareAgent.RunAsync called for game {GameId} with {MessageCount} message(s)", gameId, messages?.Count() ?? 0);
        
        if (messages != null)
        {
            foreach (var msg in messages)
            {
                _logger.LogDebug("Incoming message - Role: {Role}, Text: {Text}, AuthorName: {AuthorName}", 
                    msg.Role, msg.Text?.Substring(0, Math.Min(100, msg.Text?.Length ?? 0)) ?? "(null)", msg.AuthorName);
            }
        }
        
        AIAgent gameAgent = await GetOrCreateGameAgentAsync(cancellationToken);
        AgentThread? gameThread = await GetOrCreateGameThreadAsync(gameAgent, cancellationToken);
        
        _logger.LogInformation("Running agent for game {GameId} with thread {ThreadStatus}", gameId, gameThread != null ? "loaded" : "null");
        
        // Run with the game-specific agent and thread
        // Handle null messages case
        IEnumerable<ChatMessage> messagesToSend = messages ?? [];
        AgentRunResponse response = await gameAgent.RunAsync(messagesToSend, gameThread ?? thread, options, cancellationToken);
        
        _logger.LogInformation("Agent run completed for game {GameId}. Response contains {MessageCount} message(s)", 
            gameId, response.Messages?.Count() ?? 0);
        
        if (response.Messages != null)
        {
            foreach (var msg in response.Messages)
            {
                _logger.LogDebug("Response message - Role: {Role}, Text: {Text}, AuthorName: {AuthorName}", 
                    msg.Role, msg.Text?.Substring(0, Math.Min(100, msg.Text?.Length ?? 0)) ?? "(null)", msg.AuthorName);
            }
        }
        
        // Persist messages and thread after completion
        await PersistGameStateAsync(response, cancellationToken);
        
        _logger.LogInformation("Game state persisted for game {GameId}", gameId);
        
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
            _logger.LogError("HttpContext is not available in GetOrCreateGameAgentAsync");
            throw new InvalidOperationException("HttpContext is not available");
        }

        // Extract gameId from route
        string? gameIdStr = context.Request.RouteValues["gameId"]?.ToString();
        if (!Guid.TryParse(gameIdStr, out Guid gameId))
        {
            _logger.LogError("Invalid game ID in route: {GameIdStr}", gameIdStr);
            throw new ArgumentException("Invalid game ID in route");
        }

        // Check if we already created the agent for this request
        string cacheKey = $"GameAgent_{gameId}";
        if (context.Items.TryGetValue(cacheKey, out object? cachedAgent) && cachedAgent is AIAgent agent)
        {
            _logger.LogDebug("Using cached agent for game {GameId}", gameId);
            return agent;
        }

        _logger.LogInformation("Creating new agent for game {GameId}", gameId);

        // Get scoped services
        using IServiceScope scope = _serviceProvider.CreateScope();
        IGameService gameService = scope.ServiceProvider.GetRequiredService<IGameService>();
        IChatClient chatClient = scope.ServiceProvider.GetRequiredService<IChatClient>();
        IRulesSearchService? rulesSearchService = scope.ServiceProvider.GetService<IRulesSearchService>();
        ILogger logger = scope.ServiceProvider.GetRequiredService<ILogger<GameAwareAgent>>();

        // Get the game
        _logger.LogDebug("Fetching game data for game {GameId}", gameId);
        GameDto? gameDto = await gameService.GetGameAsync(gameId, cancellationToken);
        if (gameDto == null)
        {
            _logger.LogError("Game '{GameId}' does not exist", gameId);
            throw new ArgumentException($"Game '{gameId}' does not exist.");
        }

        _logger.LogInformation("Game found: {GameId}, Scenario: {ScenarioName}, Player: {PlayerName}", 
            gameId, gameDto.Scenario.Name, gameDto.Player.Name);
        _logger.LogDebug("System prompt length: {Length} characters", gameDto.Scenario.SystemPrompt?.Length ?? 0);

        // Create agent with game context
        // Handle null SystemPrompt
        string systemPrompt = gameDto.Scenario.SystemPrompt ?? string.Empty;
        if (string.IsNullOrWhiteSpace(systemPrompt))
        {
            _logger.LogWarning("Game {GameId} has empty SystemPrompt, using default", gameId);
            systemPrompt = "You are a helpful game master assistant.";
        }

        IChatClient instrumentedChatClient = chatClient.WrapWithInstrumentation(logger);
        AIAgent gameAgent = instrumentedChatClient.CreateJaimesAgent(
            logger,
            $"JaimesAgent-{gameId}",
            systemPrompt,
            CreateTools(gameDto, rulesSearchService));

        _logger.LogInformation("Agent created for game {GameId} with {ToolCount} tool(s)", 
            gameId, CreateTools(gameDto, rulesSearchService)?.Count ?? 0);

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
            _logger.LogWarning("Cannot persist game state - HttpContext or GameId not available");
            return;
        }

        if (!context.Items.TryGetValue("GameDto", out object? gameDtoObj) || gameDtoObj is not GameDto gameDto)
        {
            _logger.LogWarning("Cannot persist game state for game {GameId} - GameDto not available", gameId);
            return;
        }

        if (!context.Items.TryGetValue($"GameThread_{gameId}", out object? threadObj) || threadObj is not AgentThread thread)
        {
            _logger.LogWarning("Cannot persist game state for game {GameId} - Thread not available", gameId);
            return;
        }

        // Extract the new user message for persistence
        ChatMessage? newUserMessage = response.Messages?.LastOrDefault(m => m.Role == ChatRole.User);
        if (newUserMessage == null)
        {
            _logger.LogDebug("No user message found in response for game {GameId}, skipping persistence", gameId);
            return;
        }

        _logger.LogInformation("Persisting game state for game {GameId} - User message: {Text}", 
            gameId, newUserMessage.Text?.Substring(0, Math.Min(100, newUserMessage.Text?.Length ?? 0)) ?? "(null)");

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
        _logger.LogDebug("Added user message entity for game {GameId}", gameId);

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
        _logger.LogDebug("Added {Count} AI message entity/entities for game {GameId}", aiMessageEntities.Count, gameId);

        await dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Saved {TotalMessageCount} message(s) to database for game {GameId}", 
            1 + aiMessageEntities.Count, gameId);

        // Get the last AI message ID for thread association
        int? lastAiMessageId = aiMessageEntities.LastOrDefault()?.Id;

        // Serialize and save thread after completion
        string threadJson = thread.Serialize(JsonSerializerOptions.Web).GetRawText();
        _logger.LogDebug("Serialized thread for game {GameId}, length: {Length} characters", gameId, threadJson.Length);
        await chatHistoryService.SaveThreadJsonAsync(gameId, threadJson, lastAiMessageId, cancellationToken);
        _logger.LogInformation("Saved thread JSON for game {GameId}", gameId);
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

