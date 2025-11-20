using System.ClientModel;
using System.Text;
using System.Text.Json;
using Azure.AI.OpenAI;
using MattEland.Jaimes.Agents.Middleware;
using MattEland.Jaimes.Domain;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace MattEland.Jaimes.Agents.Services;

public class ChatService(
    JaimesChatOptions options,
    ILogger<ChatService> logger,
    IChatHistoryService chatHistoryService,
    IRulesSearchService? rulesSearchService = null)
    : IChatService
{
    private readonly JaimesChatOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly ILogger<ChatService> logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IChatHistoryService chatHistoryService = chatHistoryService ?? throw new ArgumentNullException(nameof(chatHistoryService));

    // Use consistent source name with OpenTelemetry configuration
    private const string DefaultActivitySourceName = "Jaimes.ApiService";

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
                name: "GetPlayerInfo",
                description: "Retrieves detailed information about the current player character in the game, including their name, unique identifier, and character description. Use this tool whenever you need to reference or describe the player character, their background, or their current state in the game world.");
            toolList.Add(playerInfoFunction);
            
            // Add rules search tool if the service is available
            if (rulesSearchService != null)
            {
                RulesSearchTool rulesSearchTool = new(game, rulesSearchService);
                
                AIFunction rulesSearchFunction = AIFunctionFactory.Create(
                    (string query) => rulesSearchTool.SearchRulesAsync(query),
                    name: "SearchRules",
                    description: "Searches the ruleset's indexed rules to find answers to specific questions or queries. This is a rules search tool that gets answers from rules to specific questions or queries. Use this tool whenever you need to look up game rules, mechanics, or rule clarifications. The tool will search through the indexed rules for the current scenario's ruleset and return relevant information.");
                toolList.Add(rulesSearchFunction);
            }
            
            tools = toolList;
            
            // Log detailed information about registered tools for debugging
            foreach (AITool tool in tools)
            {
                logger.LogInformation(
                    "Tool registered - Name: {ToolName}, Description: {ToolDescription}, Type: {ToolType}",
                    tool.Name,
                    tool.Description,
                    tool.GetType().Name);
                
                // Log the full tool object for debugging
                logger.LogDebug("Full tool details: {ToolDetails}", JsonSerializer.Serialize(tool, new JsonSerializerOptions { WriteIndented = true }));
            }
            
            logger.LogInformation(
                "Agent created with {ToolCount} tool(s) available. Tool names: {ToolNames}",
                tools.Count,
                string.Join(", ", tools.Select(t => t.Name)));
        }
        else
        {
            logger.LogWarning("Agent created with no tools available");
        }

        // Per Microsoft docs: https://learn.microsoft.com/en-us/agent-framework/user-guide/agents/agent-observability?pivots=programming-language-csharp
        // First instrument the chat client, then use it to create the agent
        // Per Microsoft docs: https://learn.microsoft.com/en-us/agent-framework/user-guide/agents/agent-middleware?pivots=programming-language-csharp
        // Register IChatClient middleware on the chat client before creating the agent
        
        IChatClient instrumentedChatClient = new AzureOpenAIClient(
                new Uri(_options.Endpoint),
                new ApiKeyCredential(_options.ApiKey))
            .GetChatClient(_options.TextGenerationDeployment)
            .AsIChatClient()
            .AsBuilder()
            .UseOpenTelemetry(
                sourceName: DefaultActivitySourceName,
                configure: cfg => cfg.EnableSensitiveData = true)
            .Use(
                getResponseFunc: ChatClientMiddleware.Create(logger),
                getStreamingResponseFunc: null)
            .Build();
        
        logger.LogInformation("Chat client instrumented with OpenTelemetry and chat client middleware (source: {SourceName})", DefaultActivitySourceName);
        
        // Create the agent with the instrumented chat client and enable observability
        // Per Microsoft docs, use WithOpenTelemetry on the agent (not UseOpenTelemetry on builder)
        // Per Microsoft docs: https://learn.microsoft.com/en-us/agent-framework/user-guide/agents/agent-middleware?pivots=programming-language-csharp
        // Register agent run middleware on the agent builder
        AIAgent agent = new ChatClientAgent(
                instrumentedChatClient,
                name: $"JaimesAgent-{game!.GameId}",
                instructions: systemPrompt,
                tools: tools)
            .AsBuilder()
            .UseOpenTelemetry(
                sourceName: DefaultActivitySourceName,
                configure: cfg => cfg.EnableSensitiveData = true)
            .Use(runFunc: AgentRunMiddleware.CreateRunFunc(logger), runStreamingFunc: null)
            .Use(ToolInvocationMiddleware.Create(logger))
            .Build();
        
        logger.LogInformation("Agent created with OpenTelemetry, agent run middleware, and tool invocation middleware");
        
        return agent;
    }

    public async Task<JaimesChatResponse> ProcessChatMessageAsync(GameDto game, string message, CancellationToken cancellationToken = default)
    {
        // Build the Azure OpenAI client from options with tools
        AIAgent agent = CreateAgent(game.Scenario.SystemPrompt, game);

        AgentThread? thread = null;
        
        // Get the thread from the database
        string? existingThreadJson = await chatHistoryService.GetMostRecentThreadJsonAsync(game.GameId, cancellationToken);
        if (!string.IsNullOrEmpty(existingThreadJson))
        {
            JsonElement jsonElement = JsonSerializer.Deserialize<JsonElement>(existingThreadJson, JsonSerializerOptions.Web);
            thread = agent.DeserializeThread(jsonElement, JsonSerializerOptions.Web);
        }

        thread ??= agent.GetNewThread();

        // Log the thread before the chat for diagnostics
        string json = thread.Serialize(JsonSerializerOptions.Web).GetRawText();
        logger.LogInformation("Thread before Chat: {Thread}", json);

        AgentRunResponse response = await agent.RunAsync(message, thread, cancellationToken: cancellationToken);

        // Serialize the thread after the chat
        json = thread.Serialize(JsonSerializerOptions.Web).GetRawText();
        logger.LogInformation("Thread after Chat: {Thread}", json);

        // Create MessageResponse array for AI responses
        MessageResponse[] responseMessages = response.Messages.Select(m => new MessageResponse
        {
            Text = m.Text,
            Participant = ChatParticipant.GameMaster,
            PlayerId = null,
            ParticipantName = "Game Master",
            CreatedAt = DateTime.UtcNow
        }).ToArray();

        return new JaimesChatResponse
        {
            Messages = responseMessages,
            ThreadJson = json
        };
    }

    public async Task<InitialMessageResponse> GenerateInitialMessageAsync(GenerateInitialMessageRequest request, CancellationToken cancellationToken = default)
    {
        // Create a minimal GameDto for tool registration
        // Note: Some fields are not available in the request, so we use placeholders
        GameDto gameForTools = new GameDto
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
                SystemPrompt = request.SystemPrompt,
                NewGameInstructions = request.NewGameInstructions,
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

        // Build the initial prompt with new game instructions
        // Player character info is now available via GetPlayerInfo tool call, so it's not included in the prompt
        StringBuilder promptBuilder = new();
        promptBuilder.AppendLine(request.NewGameInstructions);
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Please begin the adventure with an opening message that introduces the scenario and sets the scene for the player.");

        string initialPrompt = promptBuilder.ToString();

        // Log the thread before the chat for diagnostics
        string json = thread.Serialize(JsonSerializerOptions.Web).GetRawText();
        logger.LogInformation("Thread before Initial Message: {Thread}", json);

        // Generate the initial message
        AgentRunResponse response = await agent.RunAsync(initialPrompt, thread, cancellationToken: cancellationToken);

        // Serialize the thread after the chat
        json = thread.Serialize(JsonSerializerOptions.Web).GetRawText();
        logger.LogInformation("Thread after Initial Message: {Thread}", json);

        // Get the first message from the response
        string? messageText = response.Messages.FirstOrDefault()?.Text;
        string firstMessage = string.IsNullOrWhiteSpace(messageText) ? "Welcome to the adventure!" : messageText;

        return new InitialMessageResponse
        {
            Message = firstMessage,
            ThreadJson = json
        };
    }
}
