namespace MattEland.Jaimes.Agents.Middleware;

/// <summary>
/// Middleware that tracks agent runs and logs them with OpenTelemetry.
/// This middleware intercepts agent run execution to track agent interactions.
/// </summary>
public static class AgentRunMiddleware
{
    private static readonly ActivitySource ActivitySource = new("Jaimes.Agents.Run");

    /// <summary>
    /// Creates an agent run middleware that tracks agent run execution.
    /// </summary>
    /// <param name="logger">The logger to use for logging agent runs.</param>
    /// <returns>A middleware function that can be used with the agent builder.</returns>
    public static
        Func<IEnumerable<ChatMessage>, AgentThread?, AgentRunOptions?, AIAgent, CancellationToken,
            Task<AgentRunResponse>>
        CreateRunFunc(ILogger logger)
    {
        // Log that middleware is being created/registered
        logger.LogInformation("AgentRunMiddleware created and registered");

        return async (messages, thread, options, innerAgent, cancellationToken) =>
        {
            int messageCount = messages.Count();
            string agentName = innerAgent.Name ?? "unknown";

            // Log incoming message text at the beginning
            ChatMessage? lastUserMessage = messages.LastOrDefault(m => m.Role == ChatRole.User);
            if (lastUserMessage != null && !string.IsNullOrEmpty(lastUserMessage.Text))
            {
                string messagePreview = lastUserMessage.Text.Length > 500
                    ? lastUserMessage.Text.Substring(0, 500) + "..."
                    : lastUserMessage.Text;
                logger.LogInformation(
                    "📥 Incoming message text: {MessageText}",
                    messagePreview);
            }

            // Log that an agent run is starting
            logger.LogInformation(
                "🤖 Agent run started: {AgentName} with {MessageCount} message(s)",
                agentName,
                messageCount);

            // Log run details for debugging (guard expensive serialization)
            if (logger.IsEnabled(LogLevel.Debug))
            {
                string optionsText;
                try
                {
                    optionsText = options != null ? JsonSerializer.Serialize(options) : "null";
                }
                catch (Exception ex)
                {
                    optionsText = $"<options serialization failed: {ex.Message}>";
                }

                string threadStatus = thread != null ? "existing" : "new";
                logger.LogDebug(
                    "Agent run context - AgentName: {AgentName}, MessageCount: {MessageCount}, ThreadStatus: {ThreadStatus}, Options: {Options}",
                    agentName,
                    messageCount,
                    threadStatus,
                    optionsText);
            }

            // Create an OpenTelemetry activity for the agent run
            using Activity? activity = ActivitySource.StartActivity($"Agent.Run.{agentName}");

            if (activity != null)
            {
                activity.SetTag("agent.name", agentName);
                activity.SetTag("agent.message_count", messageCount);
                activity.SetTag("agent.thread_status", thread != null ? "existing" : "new");

                try
                {
                    activity.SetTag("agent.options", options != null ? JsonSerializer.Serialize(options) : "null");
                }
                catch (Exception ex)
                {
                    activity.SetTag("agent.options", $"<options serialization failed: {ex.Message}>");
                }
            }

            AgentRunResponse? response = null;
            Exception? exception = null;

            try
            {
                // Execute the actual agent run
                response = await innerAgent.RunAsync(messages, thread, options, cancellationToken);

                int responseMessageCount = response.Messages?.Count() ?? 0;

                // Log successful run
                logger.LogInformation(
                    "Agent run completed: {AgentName} with {ResponseMessageCount} response message(s)",
                    agentName,
                    responseMessageCount);

                if (activity != null)
                {
                    activity.SetTag("agent.response_message_count", responseMessageCount);
                    activity.SetStatus(ActivityStatusCode.Ok);
                }
            }
            catch (Exception ex)
            {
                exception = ex;
                logger.LogError(
                    ex,
                    "Agent run failed: {AgentName}",
                    agentName);

                if (activity != null)
                {
                    activity.SetTag("agent.error", ex.Message);
                    activity.SetStatus(ActivityStatusCode.Error, ex.Message);
                }

                throw;
            }
            finally
            {
                if (activity != null && exception == null && response != null)
                {
                    // Log response summary if available
                    string? responseSummary = response.Messages?.FirstOrDefault()?.Text;
                    if (!string.IsNullOrEmpty(responseSummary))
                    {
                        // Truncate long responses for logging
                        if (responseSummary.Length > 500) responseSummary = responseSummary[..500] + "... (truncated)";
                        activity.SetTag("agent.response_summary", responseSummary);

                        // Log the response text prominently
                        logger.LogInformation(
                            "📤 Response message text: {ResponseText}",
                            responseSummary);
                    }
                }
            }

            return response!;
        };
    }

    /// <summary>
    /// Creates an agent run streaming middleware that tracks agent run execution.
    /// </summary>
    /// <param name="logger">The logger to use for logging agent runs.</param>
    /// <returns>A middleware function that can be used with the agent builder.</returns>
    public static
        Func<IEnumerable<ChatMessage>, AgentThread?, AgentRunOptions?, AIAgent, CancellationToken,
            IAsyncEnumerable<AgentRunResponseUpdate>>
        CreateStreamingRunFunc(ILogger logger)
    {
        return (messages, thread, options, innerAgent, cancellationToken) =>
        {
            string agentName = innerAgent.Name ?? "unknown";
            int messageCount = messages.Count();

            logger.LogInformation(
                "🤖 Agent streaming run started: {AgentName} with {MessageCount} message(s)",
                agentName,
                messageCount);

            return innerAgent.RunStreamingAsync(messages, thread, options, cancellationToken);
        };
    }
}