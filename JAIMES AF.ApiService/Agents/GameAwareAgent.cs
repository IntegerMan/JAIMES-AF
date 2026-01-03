using System.Text.Json;
using MattEland.Jaimes.Agents.Helpers;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.ServiceDefaults;
using MattEland.Jaimes.Tools;
using Microsoft.Extensions.AI;

namespace MattEland.Jaimes.ApiService.Agents;

/// <summary>
/// A wrapper agent that creates game-specific agents based on the request route.
/// </summary>
public class GameAwareAgent(
    IServiceProvider serviceProvider,
    IHttpContextAccessor httpContextAccessor,
    ILogger<GameAwareAgent> logger) : AIAgent

{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private readonly ILogger<GameAwareAgent> _logger = logger;

    private readonly IMessageUpdateNotifier _messageUpdateNotifier =
        serviceProvider.GetRequiredService<IMessageUpdateNotifier>();

    public override async Task<AgentRunResponse> RunAsync(
        IEnumerable<ChatMessage> messages,
        AgentThread? thread = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        List<ChatMessage> messagesList = messages.ToList();
        AIAgent gameAgent = await GetOrCreateGameAgentAsync(cancellationToken);
        AgentThread? gameThread = await GetOrCreateGameThreadAsync(gameAgent, cancellationToken);

        AgentRunResponse response = await gameAgent.RunAsync(messagesList, gameThread, options, cancellationToken);

        _ = await PersistGameStateAsync(messagesList, response, gameThread, cancellationToken);

        return response;
    }

    public override async IAsyncEnumerable<AgentRunResponseUpdate> RunStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentThread? thread = null,
        AgentRunOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        AIAgent gameAgent = await GetOrCreateGameAgentAsync(cancellationToken);
        AgentThread? gameThread = await GetOrCreateGameThreadAsync(gameAgent, cancellationToken);

        List<ChatMessage> messagesList = messages.ToList();
        Dictionary<string, AgentRunResponseUpdate> assistantUpdatesByMessageId = new();
        AgentRunResponseUpdate? lastUpdate = null;
        await foreach (AgentRunResponseUpdate update in gameAgent.RunStreamingAsync(messagesList,
                           gameThread ?? thread,
                           options,
                           cancellationToken))
        {
            lastUpdate = update;

            if (update.Role == ChatRole.Assistant && !string.IsNullOrEmpty(update.Text))
            {
                string messageKey = update.MessageId ?? "default";

                if (assistantUpdatesByMessageId.TryGetValue(messageKey, out var existingUpdate))
                {
                    // Accumulate text for persistence by adding contents to our stored update.
                    // We avoid modifying the 'update' object itself as it is yielded back to the caller
                    // which expects it to be a delta.
                    foreach (var content in update.Contents)
                    {
                        existingUpdate.Contents.Add(content);
                    }

                    existingUpdate.AuthorName = update.AuthorName ?? existingUpdate.AuthorName;
                    existingUpdate.ResponseId = update.ResponseId;
                }
                else
                {
                    // Create a new cumulative update for this message ID
                    AgentRunResponseUpdate cumulativeUpdate = new()
                    {
                        Role = update.Role,
                        MessageId = update.MessageId,
                        AuthorName = update.AuthorName,
                        ResponseId = update.ResponseId
                    };
                    foreach (var content in update.Contents)
                    {
                        cumulativeUpdate.Contents.Add(content);
                    }

                    assistantUpdatesByMessageId[messageKey] = cumulativeUpdate;
                }
            }

            yield return update;
        }

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

        (int? userMessageId, List<int> assistantMessageIds) = await PersistGameStateAsync(messagesList,
            finalResponse,
            gameThread ?? thread,
            cancellationToken);

        yield return new AgentRunResponseUpdate
        {
            Role = ChatRole.System,
            Contents =
            {
                new TextContent(JsonSerializer.Serialize(new
                {
                    Type = "MessagePersisted",
                    UserMessageId = userMessageId,
                    AssistantMessageIds = assistantMessageIds
                }))
            },
            MessageId = "persistence-complete"
        };
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

        string cacheKey = $"GameAgent_{gameId}";
        if (context.Items.TryGetValue(cacheKey, out object? cachedAgent) && cachedAgent is AIAgent agent)
            return agent;

        IServiceProvider requestServices = context.RequestServices;
        IGameService gameService = requestServices.GetRequiredService<IGameService>();
        IInstructionService instructionService = requestServices.GetRequiredService<IInstructionService>();
        IChatClient chatClient = requestServices.GetRequiredService<IChatClient>();
        ILogger scopedLogger = requestServices.GetRequiredService<ILogger<GameAwareAgent>>();

        GameDto? gameDto = await gameService.GetGameAsync(gameId, cancellationToken);
        if (gameDto == null)
        {
            _logger.LogError("Game '{GameId}' does not exist", gameId);
            throw new ArgumentException($"Game '{gameId}' does not exist.");
        }

        _logger.LogInformation("Game found: {GameId}, Scenario: {ScenarioName}, Player: {PlayerName}",
            gameId,
            gameDto.Scenario.Name,
            gameDto.Player.Name);

        // Get instructions from InstructionService
        string? systemPrompt = await instructionService.GetInstructionsForGameAsync(gameId, cancellationToken);
        if (string.IsNullOrWhiteSpace(systemPrompt))
        {
            _logger.LogWarning("Game {GameId} has no instructions configured, using default", gameId);
            systemPrompt = "You are a helpful game master assistant.";
        }

        IList<AITool>? tools = CreateTools(gameDto, requestServices);
        IChatClient instrumentedChatClient = chatClient.WrapWithInstrumentation(scopedLogger);
        AIAgent gameAgent = instrumentedChatClient.CreateJaimesAgent(
            scopedLogger,
            $"JaimesAgent-{gameId}",
            systemPrompt,
            tools,
            () => _httpContextAccessor.HttpContext?.RequestServices);

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
            string? existingThreadJson =
                await chatHistoryService.GetMostRecentThreadJsonAsync(gameId, cancellationToken);
            if (!string.IsNullOrEmpty(existingThreadJson))
            {
                JsonElement jsonElement =
                    JsonSerializer.Deserialize<JsonElement>(existingThreadJson, JsonSerializerOptions.Web);
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
            GameConversationMemoryProviderFactory memoryProviderFactory =
                factoryScope.ServiceProvider.GetRequiredService<GameConversationMemoryProviderFactory>();
            memoryProvider = memoryProviderFactory.CreateForGame(gameId, _serviceProvider);
        }

        memoryProvider.SetThread(gameThread);

        string memoryProviderKey = $"MemoryProvider_{gameId}";
        context.Items[memoryProviderKey] = memoryProvider;
        context.Items[cacheKey] = gameThread;

        return gameThread;
    }

    private async Task<(int? UserMessageId, List<int> AssistantMessageIds)> PersistGameStateAsync(
        IEnumerable<ChatMessage> incomingMessages,
        AgentRunResponse response,
        AgentThread? thread,
        CancellationToken cancellationToken)
    {
        HttpContext? context = _httpContextAccessor.HttpContext;
        if (context == null || !context.Items.TryGetValue("GameId", out object? gameIdObj) ||
            gameIdObj is not Guid gameId)
        {
            _logger.LogWarning("Cannot persist game state - HttpContext or GameId not available");
            return (null, new List<int>());
        }

        if (!context.Items.TryGetValue("GameDto", out object? gameDtoObj) || gameDtoObj is not GameDto gameDto)
        {
            _logger.LogWarning("Cannot persist game state for game {GameId} - GameDto not available", gameId);
            return (null, new List<int>());
        }

        if (thread == null)
        {
            AIAgent? gameAgent =
                context.Items.TryGetValue($"GameAgent_{gameId}", out object? agentObj) && agentObj is AIAgent agent
                    ? agent
                    : null;
            if (gameAgent != null)
            {
                thread = gameAgent.GetNewThread();
            }
            else
            {
                _logger.LogWarning("Cannot create thread for persistence - agent not available for game {GameId}",
                    gameId);
                return (null, new List<int>());
            }
        }

        ChatMessage? newUserMessage = incomingMessages?.LastOrDefault(m => m.Role == ChatRole.User);
        if (newUserMessage == null)
            return (null, new List<int>());

        // Get scoped services
        using IServiceScope scope = _serviceProvider.CreateScope();
        IDbContextFactory<JaimesDbContext> contextFactory =
            scope.ServiceProvider.GetRequiredService<IDbContextFactory<JaimesDbContext>>();
        IChatHistoryService chatHistoryService = scope.ServiceProvider.GetRequiredService<IChatHistoryService>();
        TextGenerationModelOptions? modelOptions = scope.ServiceProvider.GetService<TextGenerationModelOptions>();

        // Persist messages to database
        await using JaimesDbContext dbContext = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Get the model entity if model options are available
        Model? model = null;
        if (modelOptions != null)
        {
            model = await dbContext.GetOrCreateModelAsync(
                modelOptions.Name,
                modelOptions.Provider.ToString(),
                modelOptions.Endpoint,
                _logger,
                cancellationToken);
        }

        // Get the scenario's agent and instruction version for linking messages
        ScenarioAgent? scenarioAgent = await dbContext.ScenarioAgents
            .AsNoTracking()
            .FirstOrDefaultAsync(sa => sa.ScenarioId == gameDto.Scenario.Id, cancellationToken);

        if (scenarioAgent == null)
        {
            throw new InvalidOperationException($"Scenario '{gameDto.Scenario.Id}' has no associated agent.");
        }

        // Use the effective agent ID and version ID from the game DTO (which includes fallbacks)
        string agentId = gameDto.AgentId ?? scenarioAgent.AgentId;
        int? resolvedInstructionVersionId = gameDto.InstructionVersionId ??
                                            (gameDto.AgentId == null ? scenarioAgent.InstructionVersionId : null);

        // If version is null (Dynamic/Latest), resolve it now
        if (!resolvedInstructionVersionId.HasValue)
        {
            // We need to look up the latest active version for the agent
            var latestVersion = await dbContext.AgentInstructionVersions
                .AsNoTracking()
                .Where(v => v.AgentId == agentId && v.IsActive)
                .OrderByDescending(v => v.CreatedAt)
                .Select(v => new {v.Id})
                .FirstOrDefaultAsync(cancellationToken);

            if (latestVersion != null)
            {
                resolvedInstructionVersionId = latestVersion.Id;
            }
            else
            {
                // Try finding any version for the current agent before falling back to the scenario default
                var anyVersion = await dbContext.AgentInstructionVersions
                    .AsNoTracking()
                    .Where(v => v.AgentId == agentId)
                    .OrderByDescending(v => v.CreatedAt)
                    .Select(v => new {v.Id})
                    .FirstOrDefaultAsync(cancellationToken);

                if (anyVersion != null)
                {
                    resolvedInstructionVersionId = anyVersion.Id;
                }
                else
                {
                    // Fallback to scenario default if resolution fails (safety net). 
                    // If we fall back to the scenario's version, we MUST also use the scenario's agent ID
                    // so that the AgentId and InstructionVersionId remain consistent.
                    _logger.LogWarning(
                        "Agent {AgentId} has no instruction versions. Falling back to scenario agent {ScenarioAgentId} and version {VersionId}",
                        agentId,
                        scenarioAgent.AgentId,
                        scenarioAgent.InstructionVersionId);
                    resolvedInstructionVersionId = scenarioAgent.InstructionVersionId;
                    agentId = scenarioAgent.AgentId;
                }
            }
        }

        if (!resolvedInstructionVersionId.HasValue)
        {
            throw new InvalidOperationException(
                $"Could not resolve an active instruction version for agent '{agentId}'.");
        }

        int instructionVersionId = resolvedInstructionVersionId.Value;

        // Determine context agent (Agent ID and Version) for the User Message
        // User requests that User messages inherit these from the message being replied to (the last message in the game)
        // This effectively captures "who the user is reacting to"

        string userMessageAgentId;
        int userMessageInstructionVersionId;

        // Check for the last existing message in the game
        var lastMessageEntry = await dbContext.Messages
            .AsNoTracking()
            .Where(m => m.GameId == gameId)
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .Select(m => new {m.AgentId, m.InstructionVersionId})
            .FirstOrDefaultAsync(cancellationToken);
        if (lastMessageEntry != null)
        {
            userMessageAgentId = lastMessageEntry.AgentId;
            userMessageInstructionVersionId = lastMessageEntry.InstructionVersionId;
        }

        else
        {
            // Fallback to scenario default if no previous message (should be rare due to initial greeting)
            userMessageAgentId = agentId;
            userMessageInstructionVersionId = instructionVersionId;
        }

        // Save user message
        // Note: AgentId and InstructionVersionId are now required and populated from context
        Message userMessageEntity = new()
        {
            GameId = gameId,
            Text = newUserMessage.Text ?? string.Empty,
            PlayerId = gameDto.Player.Id,
            CreatedAt = DateTime.UtcNow,
            AgentId = userMessageAgentId,
            InstructionVersionId = userMessageInstructionVersionId
        };

        dbContext.Messages.Add(userMessageEntity);

        List<Message> aiMessageEntities = (response.Messages ?? [])
            .Where(m => m.Role == ChatRole.Assistant)
            .Select(m => new Message
            {
                GameId = gameId,
                Text = m.Text ?? string.Empty,
                PlayerId = null,
                CreatedAt = DateTime.UtcNow,
                AgentId = agentId,
                InstructionVersionId = instructionVersionId,
                ModelId = model?.Id
            })
            .ToList();

        dbContext.Messages.AddRange(aiMessageEntities);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (aiMessageEntities.Count > 0)
        {
            Message lastAiMessage = aiMessageEntities.Last();
            IToolCallTracker? toolCallTracker = context?.RequestServices?.GetService<IToolCallTracker>();
            if (toolCallTracker != null)
            {
                IReadOnlyList<ToolCallRecord> toolCalls = await toolCallTracker.GetToolCallsAsync();
                if (toolCalls.Count > 0)
                {
                    // Load tools for lookup
                    var tools = await dbContext.Tools
                        .ToDictionaryAsync(t => t.Name.ToLower(), t => t.Id, cancellationToken);

                    List<MessageToolCall> toolCallEntities = toolCalls.Select(tc =>
                        {
                            tools.TryGetValue(tc.ToolName.ToLower(), out int toolIdValue);
                            int? toolId = toolIdValue > 0 ? toolIdValue : null;

                            if (toolId == null)
                            {
                                _logger.LogWarning(
                                    "Tool '{ToolName}' not found in database. Registration might be missing.",
                                    tc.ToolName);
                            }

                            return new MessageToolCall
                            {
                                MessageId = lastAiMessage.Id,
                                ToolName = tc.ToolName,
                                InputJson = tc.InputJson,
                                OutputJson = tc.OutputJson,
                                CreatedAt = tc.CreatedAt,
                                InstructionVersionId = instructionVersionId,
                                ToolId = toolId
                            };
                        })
                        .ToList();

                    dbContext.MessageToolCalls.AddRange(toolCallEntities);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    await toolCallTracker.ClearAsync();

                    try
                    {
                        await _messageUpdateNotifier.NotifyToolCallsProcessedAsync(
                            lastAiMessage.Id,
                            gameId,
                            true,
                            lastAiMessage.Text,
                            cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Failed to notify clients of tool calls for message {MessageId}",
                            lastAiMessage.Id);
                    }
                }
            }
            else
            {
                _logger.LogWarning(
                    "IToolCallTracker not found in request services for message {MessageId} in game {GameId}",
                    lastAiMessage.Id,
                    gameId);
            }
        }

        // Link messages in chronological order (PreviousMessageId/NextMessageId)
        // Query all messages for the game, ordered by CreatedAt then Id to ensure consistent ordering
        List<Message> allMessages = await dbContext.Messages
            .Where(m => m.GameId == gameId)
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .ToListAsync(cancellationToken);

        // Link messages sequentially
        for (int i = 0;
             i < allMessages.Count;
             i++)
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

        IMessagePublisher messagePublisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();

        if (newUserMessage.Role != ChatRole.Tool)
        {
            ConversationMessageQueuedMessage userQueueMessage = new()
            {
                MessageId = userMessageEntity.Id,
                GameId = gameId,
                Role = ChatRole.User
            };
            await messagePublisher.PublishAsync(userQueueMessage, cancellationToken);
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
        }

// Get the last AI message ID for thread association
        int? lastAiMessageId = aiMessageEntities.LastOrDefault()?.Id;

// Use memory provider to persist thread state
// The memory provider provides a consistent interface for thread persistence
        string memoryProviderKey = $"MemoryProvider_{gameId}";
        if (context != null && context.Items.TryGetValue(memoryProviderKey, out object? providerObj) &&
            providerObj is GameConversationMemoryProvider memoryProvider)
        {
            memoryProvider.SetThread(thread);
            await memoryProvider.SaveThreadStateManuallyAsync(thread, lastAiMessageId, cancellationToken);
        }
        else
        {
            string threadJson = thread.Serialize(JsonSerializerOptions.Web).GetRawText();
            await chatHistoryService.SaveThreadJsonAsync(gameId, threadJson, lastAiMessageId, cancellationToken);
        }

        return (userMessageEntity.Id, aiMessageEntities.Select(m => m.Id).ToList());
    }

    private IList<AITool>? CreateTools(GameDto game, IServiceProvider requestServices)
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
        IRulesSearchService? rulesSearchService = requestServices.GetService<IRulesSearchService>();
        if (rulesSearchService != null)
        {
            RulesSearchTool rulesSearchTool = new(game, requestServices);
            AIFunction rulesSearchFunction = AIFunctionFactory.Create(
                (string query) => rulesSearchTool.SearchRulesAsync(query),
                "SearchRules",
                "Searches the ruleset's indexed rules to find answers to specific questions or queries. This is a rules search tool that gets answers from rules to specific questions or queries. Use this tool whenever you need to look up game rules, mechanics, or rule clarifications. The tool will search through the indexed rules for the current scenario's ruleset and return relevant information.");
            toolList.Add(rulesSearchFunction);
        }

        // Add conversation search tool if the service is available
        IConversationSearchService? conversationSearchService =
            requestServices.GetService<IConversationSearchService>();
        if (conversationSearchService != null)
        {
            ConversationSearchTool conversationSearchTool = new(game, requestServices);

            AIFunction conversationSearchFunction = AIFunctionFactory.Create(
                (string query) => conversationSearchTool.SearchConversationsAsync(query),
                "SearchConversations",
                "Searches the game's conversation history to find relevant past messages. This tool uses semantic search to find conversation messages from the current game that match your query. Results include the matched message along with the previous and next messages for context. Use this tool whenever you need to recall what was said earlier in the conversation, what the player mentioned, or any past events discussed in the game.");
            toolList.Add(conversationSearchFunction);
        }

        // Add player sentiment tool if database context factory is available
        IDbContextFactory<JaimesDbContext>? dbContextFactory =
            requestServices.GetService<IDbContextFactory<JaimesDbContext>>();
        if (dbContextFactory != null)
        {
            PlayerSentimentTool playerSentimentTool = new(game, requestServices);

            AIFunction playerSentimentFunction = AIFunctionFactory.Create(
                () => playerSentimentTool.GetRecentSentimentsAsync(),
                "GetPlayerSentiment",
                "Retrieves the last 5 most recent sentiment analysis results for the player in the current game. This helps understand the player's frustration level and emotional state. Use this tool when you need to gauge how the player is feeling about the game or recent interactions.");
            toolList.Add(playerSentimentFunction);
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

