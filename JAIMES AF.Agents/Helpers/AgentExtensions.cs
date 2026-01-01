namespace MattEland.Jaimes.Agents.Helpers;

public static class AgentExtensions
{
    public const string DefaultActivitySourceName = "Jaimes.ApiService";

    public static IChatClient WrapWithInstrumentation(this IChatClient client, ILogger logger)
    {
        // Per Microsoft docs: https://learn.microsoft.com/en-us/agent-framework/user-guide/agents/agent-observability?pivots=programming-language-csharp
        // First instrument the chat client, then use it to create the agent
        // Per Microsoft docs: https://learn.microsoft.com/en-us/agent-framework/user-guide/agents/agent-middleware?pivots=programming-language-csharp
        // Register IChatClient middleware on the chat client before creating the agent

        // Use the injected chat client and instrument it
        return client
            .AsBuilder()
            .UseOpenTelemetry(
                sourceName: DefaultActivitySourceName,
                configure: cfg => cfg.EnableSensitiveData = true)
            .Use(
                ChatClientMiddleware.Create(logger),
                ChatClientMiddleware.CreateStreaming(logger))
            .Build();
    }

    public static AIAgent CreateJaimesAgent(this IChatClient client,
        ILogger logger,
        string name,
        string prompt,
        IList<AITool>? tools = null,
        Func<IServiceProvider?>? getServiceProvider = null)
    {
        return new ChatClientAgent(client, name: name, instructions: prompt, tools: tools)
            .AsBuilder()
            .UseOpenTelemetry(DefaultActivitySourceName,
                cfg => cfg.EnableSensitiveData = true)
            .Use(AgentRunMiddleware.CreateRunFunc(logger), AgentRunMiddleware.CreateStreamingRunFunc(logger))
            .Use(ToolInvocationMiddleware.Create(logger, getServiceProvider))
            .Build();
    }
}
