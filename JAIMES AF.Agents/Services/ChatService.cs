using System.ClientModel;
using System.Text;
using System.Text.Json;
using Azure.AI.OpenAI;
using MattEland.Jaimes.Domain;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using OpenAI;

namespace MattEland.Jaimes.Agents.Services;

public class ChatService(ChatOptions options, ILogger<ChatService> logger, IChatHistoryService chatHistoryService) : IChatService
{
    private readonly ChatOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    public async Task<ChatThreadResponse> GetChatResponseAsync(GameDto game, string message, CancellationToken cancellationToken = default)
    {
        // Build the Azure OpenAI client from options
        AIAgent agent = new AzureOpenAIClient(
                new Uri(_options.Endpoint),
                new ApiKeyCredential(_options.ApiKey))
            .GetChatClient(_options.Deployment)
            .CreateAIAgent(instructions: "You are a dungeon master working with a human player for a solo adventure.") // TODO: From scenario
            .AsBuilder()
            .UseOpenTelemetry(sourceName: "agent-framework-source")
            .Build();

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

        // Return the messages and thread JSON
        return new ChatThreadResponse
        {
            Messages = response.Messages.Select(m => m.Text).ToArray(),
            ThreadJson = json
        };
    }

    public async Task<ChatResponse> ProcessChatMessageAsync(GameDto game, string message, CancellationToken cancellationToken = default)
    {
        // Get AI response
        ChatThreadResponse chatResponse = await GetChatResponseAsync(game, message, cancellationToken);

        // Create MessageResponse array for AI responses
        MessageResponse[] responseMessages = chatResponse.Messages.Select(m => new MessageResponse
        {
            Text = m,
            Participant = ChatParticipant.GameMaster,
            PlayerId = null,
            ParticipantName = "Game Master",
            CreatedAt = DateTime.UtcNow
        }).ToArray();

        return new ChatResponse
        {
            Messages = responseMessages,
            ThreadJson = chatResponse.ThreadJson
        };
    }

    public async Task<InitialMessageResponse> GenerateInitialMessageAsync(GenerateInitialMessageRequest request, CancellationToken cancellationToken = default)
    {
        // Build the Azure OpenAI client from options with the system prompt
        AIAgent agent = new AzureOpenAIClient(
                new Uri(_options.Endpoint),
                new ApiKeyCredential(_options.ApiKey))
            .GetChatClient(_options.Deployment)
            .CreateAIAgent(instructions: request.SystemPrompt)
            .AsBuilder()
            .UseOpenTelemetry(sourceName: "agent-framework-source")
            .Build();

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
