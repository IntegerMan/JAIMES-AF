using System.ClientModel;
using System.Text.Json;
using Azure.AI.OpenAI;
using MattEland.Jaimes.Domain;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using OpenAI;

namespace MattEland.Jaimes.Agents.Services;

public class ChatService(ChatOptions options, ILogger<ChatService> logger, IChatHistoryService chatHistoryService) : IChatService
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
}
