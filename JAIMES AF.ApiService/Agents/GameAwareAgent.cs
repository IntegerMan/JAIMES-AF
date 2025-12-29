using System.Text.Json;
using MattEland.Jaimes.Agents.Helpers;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.Tools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace MattEland.Jaimes.ApiService.Agents;

/// <summary>
/// A wrapper agent that creates game-specific agents based on the request route.
/// This allows us to use MapAGUI with game-specific agents.
/// </summary>
public class GameAwareAgent(
    IServiceProvider serviceProvider,
    IHttpContextAccessor httpContextAccessor,
    ILogger<GameAwareAgent> logger) : AIAgent
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private readonly ILogger<GameAwareAgent> _logger = logger;

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

        // Enumerate messages once for logging and processing
        List<ChatMessage> messagesList = (messages ?? []).ToList();

        // Log incoming messages - ensure we enumerate and log each one
        if (messagesList.Count > 0)
        {
            _logger.LogInformation("Processing {Count} incoming message(s) for game {GameId}", messagesList.Count, gameId);

            int messageIndex = 0;
            foreach (var msg in messagesList)
            {
                string textPreview = msg.Text?.Length > 200
                    ? msg.Text.Substring(0, 200) + "..."
                    : msg.Text ?? "(null)";
                _logger.LogInformation("📥 Incoming message [{Index}] - Role: {Role}, AuthorName: {AuthorName}, Text: {Text}",
                    messageIndex++, msg.Role, msg.AuthorName ?? "(none)", textPreview);
            }
        }
        else
        {
            _logger.LogWarning("No messages provided for game {GameId}", gameId);
        }

        AIAgent gameAgent = await GetOrCreateGameAgentAsync(cancellationToken);

        // Load the thread BEFORE running so the agent has conversation history
        // This ensures the agent generates responses with full context, matching what AGUI returns to the client
        AgentThread? gameThread = await GetOrCreateGameThreadAsync(gameAgent, cancellationToken);

        _logger.LogInformation("Running agent for game {GameId} with thread (has conversation history for context)", gameId);

        // Run with the game-specific agent AND the thread so it has conversation history
        // This ensures the response matches what AGUI returns to the client
        AgentRunResponse response = await gameAgent.RunAsync(messagesList, gameThread, options, cancellationToken);

        _logger.LogInformation("Agent run completed for game {GameId}. Response contains {MessageCount} message(s)",
            gameId, response.Messages?.Count() ?? 0);

        // Log outgoing messages - ensure we enumerate and log each one
        if (response.Messages != null)
        {
            List<ChatMessage> responseMessagesList = response.Messages.ToList();
            _logger.LogInformation("Processing {Count} outgoing message(s) for game {GameId}", responseMessagesList.Count, gameId);

            int messageIndex = 0;
            foreach (var msg in responseMessagesList)
            {
                string textPreview = msg.Text?.Length > 200
                    ? msg.Text.Substring(0, 200) + "..."
                    : msg.Text ?? "(null)";
                _logger.LogInformation("📤 Outgoing message [{Index}] - Role: {Role}, AuthorName: {AuthorName}, Text: {Text}",
                    messageIndex++, msg.Role, msg.AuthorName ?? "(none)", textPreview);
            }
        }
        else
        {
            _logger.LogWarning("Response messages collection is null for game {GameId}", gameId);
        }

        // Persist messages and thread after completion
        // Pass both incoming messages (for user message) and response (for assistant messages)
        // Also pass the thread so we can update it with the new conversation state
        await PersistGameStateAsync(messagesList, response, gameThread, cancellationToken);

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

        List<ChatMessage> messagesList = (messages ?? []).ToList();

        // Collect assistant messages from streaming updates for persistence
        // Group updates by MessageId to handle multiple distinct assistant messages
        // The Text property on AgentRunResponseUpdate is cumulative per message
        Dictionary<string, AgentRunResponseUpdate> assistantUpdatesByMessageId = new();
        AgentRunResponseUpdate? lastUpdate = null;

        // Stream with the game-specific agent and thread
        await foreach (AgentRunResponseUpdate update in gameAgent.RunStreamingAsync(messagesList, gameThread ?? thread, options, cancellationToken))
        {
            // Track the last update for ResponseId
            lastUpdate = update;

            // Collect assistant updates, grouping by MessageId to handle multiple messages
            if (update.Role == ChatRole.Assistant && !string.IsNullOrEmpty(update.Text))
            {
                string messageKey = update.MessageId ?? "default";
                // Keep the latest update for each message (it has the complete cumulative text)
                assistantUpdatesByMessageId[messageKey] = update;
            }

            yield return update;
        }

        // After streaming completes, build the final response from collected assistant updates
        // This ensures we persist the exact same response that was streamed to the client
        List<ChatMessage> assistantMessages = assistantUpdatesByMessageId.Values
            .Select(update => new ChatMessage(ChatRole.Assistant, update.Text)
            {
                AuthorName = update.AuthorName
            })
            .ToList();

        AgentRunResponse finalResponse = new()
        {
            Messages = assistantMessages,
            ResponseId = lastUpdate?.ResponseId
        };

        HttpContext? context = _httpContextAccessor.HttpContext;
        Guid.TryParse(context?.Request.RouteValues["gameId"]?.ToString(), out Guid gameId);

        _logger.LogInformation(
            "Streaming completed for game {GameId}. Built {MessageCount} assistant message(s) from streamed updates for persistence",
            gameId,
            assistantMessages.Count);

        await PersistGameStateAsync(messagesList, finalResponse, gameThread ?? thread, cancellationToken);
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
        IInstructionService instructionService = scope.ServiceProvider.GetRequiredService<IInstructionService>();
        IChatClient chatClient = scope.ServiceProvider.GetRequiredService<IChatClient>();
        ILogger scopedLogger = scope.ServiceProvider.GetRequiredService<ILogger<GameAwareAgent>>();

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

        // Get instructions from InstructionService
        string? systemPrompt = await instructionService.GetInstructionsAsync(gameDto.Scenario.Id, cancellationToken);
        if (string.IsNullOrWhiteSpace(systemPrompt))
        {
            _logger.LogWarning("Game {GameId} has no instructions configured, using default", gameId);
            systemPrompt = "You are a helpful game master assistant.";
        }
        else
        {
            _logger.LogDebug("System prompt length: {Length} characters", systemPrompt.Length);
        }

        // Create tools once and reuse for both agent creation and logging
        IList<AITool>? tools = CreateTools(gameDto);

        IChatClient instrumentedChatClient = chatClient.WrapWithInstrumentation(scopedLogger);
        AIAgent gameAgent = instrumentedChatClient.CreateJaimesAgent(
            scopedLogger,
            $"JaimesAgent-{gameId}",
            systemPrompt,
            tools);

        _logger.LogInformation("Agent created for game {GameId} with {ToolCount} tool(s)",
            gameId, tools?.Count ?? 0);

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

        // Get scoped service for loading thread
        AgentThread? gameThread = null;
        using (IServiceScope scope = _serviceProvider.CreateScope())
        {
            IChatHistoryService chatHistoryService = scope.ServiceProvider.GetRequiredService<IChatHistoryService>();

            // Load or create thread
            string? existingThreadJson = await chatHistoryService.GetMostRecentThreadJsonAsync(gameId, cancellationToken);
            if (!string.IsNullOrEmpty(existingThreadJson))
            {
                JsonElement jsonElement = JsonSerializer.Deserialize<JsonElement>(existingThreadJson, JsonSerializerOptions.Web);
                gameThread = agent.DeserializeThread(jsonElement, JsonSerializerOptions.Web);
            }
        }

        gameThread ??= agent.GetNewThread();

        // Create and attach memory provider to automatically persist conversation history
        // Note: The memory provider will be called manually from PersistGameStateAsync
        // since the Agent Framework API for attaching context providers may vary
        // The memory provider now uses IServiceProvider to resolve services on-demand,
        // so it can outlive the scope that created it
        // We need to resolve the factory from a scope (since it's scoped), but pass the root
        // service provider to CreateForGame so the memory provider can create its own scopes
        GameConversationMemoryProvider memoryProvider;
        using (IServiceScope factoryScope = _serviceProvider.CreateScope())
        {
            GameConversationMemoryProviderFactory memoryProviderFactory = factoryScope.ServiceProvider.GetRequiredService<GameConversationMemoryProviderFactory>();
            memoryProvider = memoryProviderFactory.CreateForGame(gameId, _serviceProvider);
        }
        memoryProvider.SetThread(gameThread);

        // Store memory provider in context for use in PersistGameStateAsync
        string memoryProviderKey = $"MemoryProvider_{gameId}";
        context.Items[memoryProviderKey] = memoryProvider;
        _logger.LogInformation("Created memory provider for game {GameId}", gameId);

        // Cache it for this request
        context.Items[cacheKey] = gameThread;

        return gameThread;
    }

    private async Task PersistGameStateAsync(IEnumerable<ChatMessage> incomingMessages, AgentRunResponse response, AgentThread? thread, CancellationToken cancellationToken)
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

        // Thread is optional - if not provided, we'll create a new one for persistence
        if (thread == null)
        {
            _logger.LogDebug("No thread provided for persistence, will create new thread for game {GameId}", gameId);
            // Get the agent to create a new thread
            AIAgent? gameAgent = context.Items.TryGetValue($"GameAgent_{gameId}", out object? agentObj) && agentObj is AIAgent agent ? agent : null;
            if (gameAgent != null)
            {
                thread = gameAgent.GetNewThread();
            }
            else
            {
                _logger.LogWarning("Cannot create thread for persistence - agent not available for game {GameId}", gameId);
                return;
            }
        }

        // Extract the new user message from incoming messages (not response)
        // The response typically only contains assistant messages
        ChatMessage? newUserMessage = incomingMessages?.LastOrDefault(m => m.Role == ChatRole.User);
        if (newUserMessage == null)
        {
            _logger.LogDebug("No user message found in incoming messages for game {GameId}, skipping persistence", gameId);
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

        // Get the scenario's agent and instruction version for linking messages
        ScenarioAgent? scenarioAgent = await dbContext.ScenarioAgents
            .AsNoTracking()
            .FirstOrDefaultAsync(sa => sa.ScenarioId == gameDto.Scenario.Id, cancellationToken);

        string? agentId = scenarioAgent?.AgentId;
        int? instructionVersionId = scenarioAgent?.InstructionVersionId;

        // Save user message
        // Note: AgentId and InstructionVersionId are null for user messages since they're not generated by agents
        Message userMessageEntity = new()
        {
            GameId = gameId,
            Text = newUserMessage.Text ?? string.Empty,
            PlayerId = gameDto.Player.Id,
            CreatedAt = DateTime.UtcNow,
            AgentId = null,
            InstructionVersionId = null
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
                CreatedAt = DateTime.UtcNow,
                AgentId = agentId,
                InstructionVersionId = instructionVersionId
            })
            .ToList();
        dbContext.Messages.AddRange(aiMessageEntities);
        _logger.LogDebug("Added {Count} AI message entity/entities for game {GameId}", aiMessageEntities.Count, gameId);

        await dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Saved {TotalMessageCount} message(s) to database for game {GameId}",
            1 + aiMessageEntities.Count, gameId);

        // Link messages in chronological order (PreviousMessageId/NextMessageId)
        // Query all messages for the game, ordered by CreatedAt then Id to ensure consistent ordering
        List<Message> allMessages = await dbContext.Messages
            .Where(m => m.GameId == gameId)
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .ToListAsync(cancellationToken);

        // Link messages sequentially
        for (int i = 0; i < allMessages.Count; i++)
        {
            Message currentMessage = allMessages[i];
            
            // Set PreviousMessageId to the previous message's Id (if exists)
            if (i > 0)
            {
                currentMessage.PreviousMessageId = allMessages[i - 1].Id;
            }

            // Set NextMessageId to the next message's Id (if exists)
            if (i < allMessages.Count - 1)
            {
                currentMessage.NextMessageId = allMessages[i + 1].Id;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogDebug("Linked {MessageCount} messages in chronological order for game {GameId}", allMessages.Count, gameId);

        // Enqueue messages for asynchronous processing (quality control, sentiment analysis)
        IMessagePublisher messagePublisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();
        
        // Enqueue user message (defensive check: ensure it's not a tool call)
        // Note: newUserMessage is already filtered to ChatRole.User, but this is a safety check
        if (newUserMessage.Role != ChatRole.Tool)
        {
            ConversationMessageQueuedMessage userQueueMessage = new()
            {
                MessageId = userMessageEntity.Id,
                GameId = gameId,
                Role = ChatRole.User
            };
            await messagePublisher.PublishAsync(userQueueMessage, cancellationToken);
            _logger.LogDebug("Enqueued user message {MessageId} for game {GameId}", userMessageEntity.Id, gameId);
        }

        // Enqueue assistant messages
        // Note: aiMessageEntities are already filtered to ChatRole.Assistant (tool calls excluded by the Where clause above)
        foreach (Message aiMessage in aiMessageEntities)
        {
            ConversationMessageQueuedMessage assistantQueueMessage = new()
            {
                MessageId = aiMessage.Id,
                GameId = gameId,
                Role = ChatRole.Assistant
            };
            await messagePublisher.PublishAsync(assistantQueueMessage, cancellationToken);
            _logger.LogDebug("Enqueued assistant message {MessageId} for game {GameId}", aiMessage.Id, gameId);
        }

        // Get the last AI message ID for thread association
        int? lastAiMessageId = aiMessageEntities.LastOrDefault()?.Id;

        // Use memory provider to persist thread state
        // The memory provider provides a consistent interface for thread persistence
        string memoryProviderKey = $"MemoryProvider_{gameId}";
        if (context.Items.TryGetValue(memoryProviderKey, out object? providerObj) && providerObj is GameConversationMemoryProvider memoryProvider)
        {
            // Update thread reference in case it changed
            memoryProvider.SetThread(thread);

            // Manually invoke the persistence logic from the memory provider
            // This provides a consistent interface for thread persistence
            await memoryProvider.SaveThreadStateManuallyAsync(thread, lastAiMessageId, cancellationToken);
        }
        else
        {
            // Fallback to direct persistence if memory provider not available
            string threadJson = thread.Serialize(JsonSerializerOptions.Web).GetRawText();
            _logger.LogDebug("Serialized thread for game {GameId}, length: {Length} characters", gameId, threadJson.Length);
            await chatHistoryService.SaveThreadJsonAsync(gameId, threadJson, lastAiMessageId, cancellationToken);
            _logger.LogInformation("Saved thread JSON for game {GameId}", gameId);
        }
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

        // Check if IRulesSearchService is available in the service provider
        // We pass the service provider to RulesSearchTool so it can resolve the service on each call
        // This avoids ObjectDisposedException when the tool outlives the scope that created it
        using IServiceScope scope = _serviceProvider.CreateScope();
        IRulesSearchService? rulesSearchService = scope.ServiceProvider.GetService<IRulesSearchService>();
        if (rulesSearchService != null)
        {
            RulesSearchTool rulesSearchTool = new(game, _serviceProvider);
            AIFunction rulesSearchFunction = AIFunctionFactory.Create(
                (string query) => rulesSearchTool.SearchRulesAsync(query),
                "SearchRules",
                "Searches the ruleset's indexed rules to find answers to specific questions or queries. This is a rules search tool that gets answers from rules to specific questions or queries. Use this tool whenever you need to look up game rules, mechanics, or rule clarifications. The tool will search through the indexed rules for the current scenario's ruleset and return relevant information.");
            toolList.Add(rulesSearchFunction);
        }

        // Add conversation search tool if the service is available
        IConversationSearchService? conversationSearchService = scope.ServiceProvider.GetService<IConversationSearchService>();
        if (conversationSearchService != null)
        {
            ConversationSearchTool conversationSearchTool = new(game, _serviceProvider);

            AIFunction conversationSearchFunction = AIFunctionFactory.Create(
                (string query) => conversationSearchTool.SearchConversationsAsync(query),
                "SearchConversations",
                "Searches the game's conversation history to find relevant past messages. This tool uses semantic search to find conversation messages from the current game that match your query. Results include the matched message along with the previous and next messages for context. Use this tool whenever you need to recall what was said earlier in the conversation, what the player mentioned, or any past events discussed in the game.");
            toolList.Add(conversationSearchFunction);
        }

        return toolList;
    }

    public override AgentThread GetNewThread()
    {
        // Delegate to the game-specific agent if available, otherwise create a new thread
        // This is called by MapAGUI, but we'll handle thread creation in GetOrCreateGameThreadAsync
        // For now, return a basic thread - MapAGUI will handle it
        HttpContext? context = _httpContextAccessor.HttpContext;
        if (context != null && context.Items.TryGetValue("GameId", out object? gameIdObj) && gameIdObj is Guid gameId)
        {
            string cacheKey = $"GameThread_{gameId}";
            if (context.Items.TryGetValue(cacheKey, out object? thread) && thread is AgentThread gameThread)
            {
                return gameThread;
            }
        }

        // Fallback - create a basic thread
        // This shouldn't normally be called since we handle thread creation in GetOrCreateGameThreadAsync
        throw new InvalidOperationException("Cannot create thread without game context");
    }

    public override AgentThread DeserializeThread(JsonElement jsonElement, JsonSerializerOptions? options = null)
    {
        // Delegate to the game-specific agent if available
        HttpContext? context = _httpContextAccessor.HttpContext;
        if (context != null && context.Items.TryGetValue("GameId", out object? gameIdObj) && gameIdObj is Guid gameId)
        {
            string cacheKey = $"GameAgent_{gameId}";
            if (context.Items.TryGetValue(cacheKey, out object? agent) && agent is AIAgent gameAgent)
            {
                return gameAgent.DeserializeThread(jsonElement, options);
            }
        }

        // Fallback - this shouldn't normally be called
        throw new InvalidOperationException("Cannot deserialize thread without game context");
    }
}

