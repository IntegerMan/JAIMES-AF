using System.ClientModel;
using System.Text.Json;
using Azure.AI.OpenAI;
using MattEland.Jaimes.Domain;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using OpenAI;

namespace MattEland.Jaimes.Agents.Services;

public class ChatService(ChatOptions options, ILogger<ChatService> logger, IChatHistoryService chatHistoryService, IGameService gameService) : IChatService
{
    private readonly ChatOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    public async Task<(string[] Messages, string ThreadJson)> GetChatResponseAsync(GameDto game, string message, CancellationToken cancellationToken = default)
    {
        // Build the Azure OpenAI client from options
        AIAgent agent = new AzureOpenAIClient(
                new Uri(_options.Endpoint),
                new ApiKeyCredential(_options.ApiKey))
            .GetChatClient(_options.Deployment)
            .CreateAIAgent(instructions: "You are a dungeon master working with a human player for a solo adventure.") // TODO: From game
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
        return (response.Messages.Select(m => m.Text).ToArray(), json);
    }

    public async Task<ChatResponse> ProcessChatMessageAsync(Guid gameId, string message, CancellationToken cancellationToken = default)
    {
        // Get the game
        GameDto? gameDto = await gameService.GetGameAsync(gameId, cancellationToken);
        if (gameDto == null)
        {
            throw new ArgumentException($"Game '{gameId}' does not exist.", nameof(gameId));
        }

        // Get AI response
        (string[] responses, string threadJson) = await GetChatResponseAsync(gameDto, message, cancellationToken);

        // Create MessageResponse array for AI responses
        MessageResponse[] responseMessages = responses.Select(m => new MessageResponse
        {
            Text = m,
            Participant = ChatParticipant.GameMaster,
            PlayerId = null,
            ParticipantName = "Game Master",
            CreatedAt = DateTime.UtcNow
        }).ToArray();

        // Create Message entities for persistence
        List<Message> messagesToPersist = [
            new() {
                GameId = gameDto.GameId,
                Text = message,
                PlayerId = gameDto.PlayerId,
                CreatedAt = DateTime.UtcNow
            }
        ];
        messagesToPersist.AddRange(responseMessages.Select(m => new Message
        {
            GameId = gameDto.GameId,
            Text = m.Text,
            PlayerId = null,
            CreatedAt = m.CreatedAt
        }));

        // Persist messages
        await gameService.AddMessagesAsync(messagesToPersist, cancellationToken);

        // Get the last AI message ID (last message where PlayerId == null)
        // After SaveChangesAsync, EF Core will have populated the Id property
        int? lastAiMessageId = messagesToPersist
            .Where(m => m.PlayerId == null)
            .LastOrDefault()?.Id;

        // Save the thread JSON
        await chatHistoryService.SaveThreadJsonAsync(gameDto.GameId, threadJson, lastAiMessageId, cancellationToken);

        return new ChatResponse
        {
            Messages = responseMessages
        };
    }
}
