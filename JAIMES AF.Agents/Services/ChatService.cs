using MattEland.Jaimes.Agents.Helpers;
using MattEland.Jaimes.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace MattEland.Jaimes.Agents.Services;

public class ChatService(
    IChatClient chatClient,
    ILogger<ChatService> logger,
    IChatHistoryService chatHistoryService,
    IServiceProvider serviceProvider,
    IConfiguration configuration)
    : IChatService
{
    private readonly IChatClient _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
    private readonly ILogger<ChatService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly IChatHistoryService _chatHistoryService =
        chatHistoryService ?? throw new ArgumentNullException(nameof(chatHistoryService));

    private readonly IServiceProvider _serviceProvider =
        serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    private readonly bool _enableSensitiveLogging =
        bool.TryParse(configuration["AI:EnableSensitiveLogging"], out bool val) && val;

    private AIAgent CreateAgent(string systemPrompt, GameDto? game = null)
    {
        // Register tools if game context is available
        // Based on Microsoft documentation: https://learn.microsoft.com/en-us/agent-framework/user-guide/agents/agent-tools?pivots=programming-language-csharp
        IList<AITool>? tools = null;
        if (game != null)
        {
            List<AITool> toolList = [];

            PlayerInfoTool playerInfoTool = new(game);

            // Create the tool with explicit name and description to ensure proper registration
            // Per Microsoft docs: https://learn.microsoft.com/en-us/agent-framework/user-guide/agents/agent-tools?pivots=programming-language-csharp
            // The Create method has optional parameters for name and description
            AIFunction playerInfoFunction = AIFunctionFactory.Create(
                () => playerInfoTool.GetPlayerInfo(),
                "GetPlayerInfo",
                "Retrieves detailed information about the current player character in the game, including their name, unique identifier, and character description. Use this tool whenever you need to reference or describe the player character, their background, or their current state in the game world.");
            toolList.Add(playerInfoFunction);

            // Add rules search tool if the service is available
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
            IConversationSearchService? conversationSearchService =
                scope.ServiceProvider.GetService<IConversationSearchService>();
            if (conversationSearchService != null)
            {
                ConversationSearchTool conversationSearchTool = new(game, _serviceProvider);

                AIFunction conversationSearchFunction = AIFunctionFactory.Create(
                    (string query) => conversationSearchTool.SearchConversationsAsync(query),
                    "SearchConversations",
                    "Searches the game's conversation history to find relevant past messages. This tool uses semantic search to find conversation messages from the current game that match your query. Results include the matched message along with the previous and next messages for context. Use this tool whenever you need to recall what was said earlier in the conversation, what the player mentioned, or any past events discussed in the game.");
                toolList.Add(conversationSearchFunction);
            }

            // Add player sentiment tool if database context factory is available
            IDbContextFactory<JaimesDbContext>? dbContextFactory =
                scope.ServiceProvider.GetService<IDbContextFactory<JaimesDbContext>>();
            if (dbContextFactory != null)
            {
                PlayerSentimentTool playerSentimentTool = new(game, _serviceProvider);

                AIFunction playerSentimentFunction = AIFunctionFactory.Create(
                    () => playerSentimentTool.GetRecentSentimentsAsync(),
                    "GetPlayerSentiment",
                    "Retrieves the last 5 most recent sentiment analysis results for the player in the current game. This helps understand the player's frustration level and emotional state. Use this tool when you need to gauge how the player is feeling about the game or recent interactions.");
                toolList.Add(playerSentimentFunction);
            }

            // Add location tracking tools if the location service is available
            ILocationService? locationService = scope.ServiceProvider.GetService<ILocationService>();
            if (locationService != null)
            {
                // Location lookup tool
                GameLocationTool gameLocationTool = new(game, _serviceProvider);

                AIFunction getLocationFunction = AIFunctionFactory.Create(
                    (string locationName) => gameLocationTool.GetLocationByNameAsync(locationName),
                    "GetLocationByName",
                    "Retrieves detailed information about a location by name, including its description/appearance, significant events that have occurred there, and nearby locations. Use this tool whenever you need to describe a location the player visits or references, or when you need to recall what has happened at a specific place. The search is case-insensitive.");
                toolList.Add(getLocationFunction);

                AIFunction getAllLocationsFunction = AIFunctionFactory.Create(
                    () => gameLocationTool.GetAllLocationsAsync(),
                    "GetAllLocations",
                    "Gets a list of all known locations in the current game with their names and brief descriptions. Use this tool when you need to see what locations have been established in the game world, or when the player asks about places they can go.");
                toolList.Add(getAllLocationsFunction);

                // Location management tool
                LocationManagementTool locationManagementTool = new(game, _serviceProvider);

                AIFunction createUpdateLocationFunction = AIFunctionFactory.Create(
                    (string name, string description, string? storytellerNotes) =>
                        locationManagementTool.CreateOrUpdateLocationAsync(name, description, storytellerNotes),
                    "CreateOrUpdateLocation",
                    "Creates a new location or updates an existing one. Use this tool to establish new places in the game world as they become relevant to the story. Every location must have a name and description / appearance. You can also add private storyteller notes that are hidden from the player to help you plan story elements.");
                toolList.Add(createUpdateLocationFunction);

                AIFunction addLocationEventFunction = AIFunctionFactory.Create(
                    (string locationName, string eventName, string eventDescription) =>
                        locationManagementTool.AddLocationEventAsync(locationName, eventName, eventDescription),
                    "AddLocationEvent",
                    "Adds a significant event to a location's history. Use this tool to record important happenings at locations - battles, discoveries, meetings, or any event worth remembering. This helps maintain narrative consistency throughout the game.");
                toolList.Add(addLocationEventFunction);

                AIFunction addNearbyLocationFunction = AIFunctionFactory.Create(
                    (string locationName,
                            string nearbyLocationName,
                            string? distance,
                            string? travelNotes,
                            string? storytellerNotes) =>
                        locationManagementTool.AddNearbyLocationAsync(locationName,
                            nearbyLocationName,
                            distance,
                            travelNotes,
                            storytellerNotes),
                    "AddNearbyLocation",
                    "Links two locations as being nearby to each other. Use this tool to establish geographic relationships between places. You can include travel information and private storyteller notes about dangers or secrets along the route that are hidden from the player.");
                toolList.Add(addNearbyLocationFunction);
            }

            tools = toolList;

            // Log detailed information about registered tools for debugging
            foreach (AITool tool in tools)
            {
                _logger.LogInformation(
                    "Tool registered - Name: {ToolName}, Description: {ToolDescription}, Type: {ToolType}",
                    tool.Name,
                    tool.Description,
                    tool.GetType().Name);

                // Log the full tool object for debugging
                _logger.LogDebug("Full tool details: {ToolDetails}",
                    JsonSerializer.Serialize(tool, new JsonSerializerOptions { WriteIndented = true }));
            }

            _logger.LogInformation(
                "Agent created with {ToolCount} tool(s) available. Tool names: {ToolNames}",
                tools.Count,
                string.Join(", ", tools.Select(t => t.Name)));
        }
        else
        {
            _logger.LogWarning("Agent created with no tools available");
        }

        IChatClient instrumentedChatClient = _chatClient.WrapWithInstrumentation(_logger, _enableSensitiveLogging);

        _logger.LogInformation(
            "Chat client instrumented with OpenTelemetry and chat client middleware (source: {SourceName})",
            AgentExtensions.DefaultActivitySourceName);

        // Create the agent with the instrumented chat client and enable observability
        // Per Microsoft docs, use WithOpenTelemetry on the agent (not UseOpenTelemetry on builder)
        // Per Microsoft docs: https://learn.microsoft.com/en-us/agent-framework/user-guide/agents/agent-middleware?pivots=programming-language-csharp
        // Register agent run middleware on the agent builder
        AIAgent agent =
            instrumentedChatClient.CreateJaimesAgent(_logger, $"JaimesAgent-{game!.GameId}", systemPrompt, tools,
                enableSensitiveData: _enableSensitiveLogging);

        _logger.LogInformation(
            "Agent created with OpenTelemetry, agent run middleware, and tool invocation middleware");

        return agent;
    }


    public async Task<InitialMessageResponse> GenerateInitialMessageAsync(GenerateInitialMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        // Create a minimal GameDto for tool registration
        // Note: Some fields are not available in the request, so we use placeholders
        GameDto gameForTools = new()
        {
            GameId = request.GameId,
            Player = new PlayerDto
            {
                Id = string.Empty, // Not available in request, but tool doesn't need it
                Name = request.PlayerName,
                Description = request.PlayerDescription,
                RulesetId = string.Empty
            },
            Scenario = new ScenarioDto
            {
                Id = string.Empty, // Not available in request
                Name = string.Empty,
                InitialGreeting = request.InitialGreeting,
                RulesetId = string.Empty
            },
            Ruleset = new RulesetDto
            {
                Id = string.Empty, // Not available in request
                Name = string.Empty
            },
            Messages = null
        };

        // Build the Azure OpenAI client from options with the system prompt and tools
        AIAgent agent = CreateAgent(request.SystemPrompt, gameForTools);

        // Create a new thread for this game
        AgentThread thread = agent.GetNewThread();

        // Create and attach memory provider for this game
        // This ensures conversation history is persisted consistently
        // We need to resolve the factory from a scope (since it's scoped), but pass the root
        // service provider to CreateForGame so the memory provider can create its own scopes
        // and outlive the scope that created it
        GameConversationMemoryProvider memoryProvider;
        using (IServiceScope factoryScope = _serviceProvider.CreateScope())
        {
            GameConversationMemoryProviderFactory memoryProviderFactory =
                factoryScope.ServiceProvider.GetRequiredService<GameConversationMemoryProviderFactory>();
            memoryProvider = memoryProviderFactory.CreateForGame(request.GameId, _serviceProvider);
        }

        memoryProvider.SetThread(thread);
        _logger.LogInformation("Created memory provider for initial message generation for game {GameId}",
            request.GameId);

        // Build the initial prompt with initial greeting
        // Player character info is now available via GetPlayerInfo tool call, so it's not included in the prompt
        StringBuilder promptBuilder = new();
        if (!string.IsNullOrWhiteSpace(request.InitialGreeting))
        {
            promptBuilder.AppendLine(request.InitialGreeting);
            promptBuilder.AppendLine();
        }

        promptBuilder.AppendLine(
            "Please begin the adventure with an opening message that introduces the scenario and sets the scene for the player.");

        string initialPrompt = promptBuilder.ToString();

        // Log the thread before the chat for diagnostics
        string json = thread.Serialize(JsonSerializerOptions.Web).GetRawText();
        _logger.LogInformation("Thread before Initial Message: {Thread}", json);

        // Generate the initial message
        AgentRunResponse response = await agent.RunAsync(initialPrompt, thread, cancellationToken: cancellationToken);

        // Serialize the thread after the chat
        json = thread.Serialize(JsonSerializerOptions.Web).GetRawText();
        _logger.LogInformation("Thread after Initial Message: {Thread}", json);

        // Persist thread state using memory provider
        await memoryProvider.SaveThreadStateManuallyAsync(thread, null, cancellationToken);
        _logger.LogInformation("Saved initial thread state for game {GameId} via memory provider", request.GameId);

        // Get the first message from the response
        string? messageText = response.Messages?.FirstOrDefault()?.Text;
        string firstMessage = string.IsNullOrWhiteSpace(messageText) ? "Welcome to the adventure!" : messageText;

        return new InitialMessageResponse
        {
            Message = firstMessage,
            ThreadJson = json
        };
    }
}