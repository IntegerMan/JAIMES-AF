using System.ClientModel;
using System.Text;
using System.Text.Json;
using Azure.AI.OpenAI;
using MattEland.Jaimes.Domain;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;

namespace MattEland.Jaimes.Agents.Services;

public class ChatService(JaimesChatOptions options, ILogger<ChatService> logger, IChatHistoryService chatHistoryService) : IChatService
{
    private readonly JaimesChatOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    private AIAgent CreateAgent(string systemPrompt, GameDto? game = null)
    {
        // Register tools if game context is available
        // Based on Microsoft documentation: https://learn.microsoft.com/en-us/agent-framework/user-guide/agents/agent-tools?pivots=programming-language-csharp
        // The documentation shows using AIFunctionFactory.Create(), but the exact namespace/API needs to be confirmed
        IList<AITool>? tools = null;
        if (game != null)
        {
            PlayerInfoTool playerInfoTool = new(game);
            tools = [AIFunctionFactory.Create(() => playerInfoTool.GetPlayerInfo())];
        }

        return new AzureOpenAIClient(
                new Uri(_options.Endpoint),
                new ApiKeyCredential(_options.ApiKey))
            .GetChatClient(_options.Deployment)
            .CreateAIAgent(instructions: systemPrompt, tools: tools)
            .AsBuilder()
            .UseOpenTelemetry(sourceName: "agent-framework-source")
            .Build();
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

        // Build the initial prompt with player character info and new game instructions
        StringBuilder promptBuilder = new();
        promptBuilder.AppendLine(request.NewGameInstructions);
        promptBuilder.AppendLine();
        promptBuilder.AppendLine($"Player Character: {request.PlayerName}");
        if (!string.IsNullOrWhiteSpace(request.PlayerDescription))
        {
            promptBuilder.AppendLine($"Character Description: {request.PlayerDescription}");
        }
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
        string firstMessage = response.Messages.FirstOrDefault()?.Text ?? "Welcome to the adventure!";

        return new InitialMessageResponse
        {
            Message = firstMessage,
            ThreadJson = json
        };
    }
}
