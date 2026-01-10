namespace MattEland.Jaimes.Agents.Helpers;

public static class AgentExtensions
{
    public const string DefaultActivitySourceName = "Jaimes.ApiService";

    /// <summary>
    /// Wraps the chat client with OpenTelemetry instrumentation and middleware.
    /// </summary>
    /// <param name="client">The chat client to wrap.</param>
    /// <param name="logger">Logger for middleware.</param>
    /// <param name="enableSensitiveData">When true, prompts and responses will be logged in telemetry. Defaults to false for security.</param>
    public static IChatClient WrapWithInstrumentation(this IChatClient client, ILogger logger,
        bool enableSensitiveData = false)
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
                configure: cfg => cfg.EnableSensitiveData = enableSensitiveData)
            .Use(
                ChatClientMiddleware.Create(logger),
                ChatClientMiddleware.CreateStreaming(logger))
            .Build();
    }

    /// <summary>
    /// Creates a JAIMES agent with OpenTelemetry instrumentation and middleware.
    /// </summary>
    /// <param name="client">The chat client to use.</param>
    /// <param name="logger">Logger for middleware.</param>
    /// <param name="name">Agent name.</param>
    /// <param name="prompt">System prompt/instructions.</param>
    /// <param name="tools">Optional tools for the agent.</param>
    /// <param name="getServiceProvider">Optional service provider factory for tool execution.</param>
    /// <param name="enableSensitiveData">When true, prompts and responses will be logged in telemetry. Defaults to false for security.</param>
    public static AIAgent CreateJaimesAgent(this IChatClient client,
        ILogger logger,
        string name,
        string prompt,
        IList<AITool>? tools = null,
        Func<IServiceProvider?>? getServiceProvider = null,
        bool enableSensitiveData = false)
    {
        return new ChatClientAgent(client, name: name, instructions: prompt, tools: tools)
            .AsBuilder()
            .UseOpenTelemetry(DefaultActivitySourceName,
                cfg => cfg.EnableSensitiveData = enableSensitiveData)
            .Use(AgentRunMiddleware.CreateRunFunc(logger), AgentRunMiddleware.CreateStreamingRunFunc(logger))
            .Use(ToolInvocationMiddleware.Create(logger, getServiceProvider))
            .Build();
    }
}

