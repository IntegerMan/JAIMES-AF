using System.ClientModel;
using Azure.AI.OpenAI;
using MattEland.Jaimes.Domain;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.Agents.AI;
using OpenAI;

namespace MattEland.Jaimes.Agents.Services;

public class ChatService(ChatOptions options) : IChatService
{
    private readonly ChatOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    public async Task<string[]> GetChatResponseAsync(GameDto game, string message, CancellationToken cancellationToken = default)
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

        // TODO: Get the thread from the database, or create a new one if it doesn't exist / can't be found
        AgentThread thread = agent.GetNewThread();

        AgentRunResponse response = await agent.RunAsync(message, thread, cancellationToken: cancellationToken);

        // TODO: Persist the thread

        return response.Messages
            .Select(m => m.Text)
            .ToArray();
    }
}
