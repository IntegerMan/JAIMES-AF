using System.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace MattEland.Jaimes.Agents.Middleware;

/// <summary>
/// Middleware that tracks IChatClient calls and logs them with OpenTelemetry.
/// This middleware intercepts chat client requests to track AI inference calls.
/// </summary>
public static class ChatClientMiddleware
{
    private static readonly ActivitySource ActivitySource = new("Jaimes.Agents.ChatClient");

    /// <summary>
    /// Creates an IChatClient middleware that tracks chat client invocations.
    /// </summary>
    /// <param name="logger">The logger to use for logging chat client invocations.</param>
    /// <returns>A middleware function that can be used with the chat client builder.</returns>
    public static Func<IEnumerable<ChatMessage>, ChatOptions?, IChatClient, CancellationToken, Task<ChatResponse>> Create(ILogger logger)
    {
        // Log that middleware is being created/registered
        logger.LogInformation("ChatClientMiddleware created and registered");
        
        return async (messages, options, innerChatClient, cancellationToken) =>
        {
            int messageCount = messages.Count();
            
            // Log that a chat client call is being made
            logger.LogInformation(
                "💬 Chat client invocation started: {MessageCount} message(s)",
                messageCount);
            
            // Log message details for debugging
            logger.LogDebug(
                "Chat client context - MessageCount: {MessageCount}, Options: {Options}",
                messageCount,
                options != null ? System.Text.Json.JsonSerializer.Serialize(options) : "null");

            // Create an OpenTelemetry activity for the chat client invocation
            using Activity? activity = ActivitySource.StartActivity("ChatClient.Invoke");
            
            if (activity != null)
            {
                activity.SetTag("chat.message_count", messageCount);
                activity.SetTag("chat.options", options != null ? System.Text.Json.JsonSerializer.Serialize(options) : "null");
            }

            ChatResponse? response = null;
            Exception? exception = null;
            
            try
            {
                // Execute the actual chat client call
                response = await innerChatClient.GetResponseAsync(messages, options, cancellationToken);
                
                int responseMessageCount = response.Messages?.Count() ?? 0;
                
                // Log successful invocation
                logger.LogInformation(
                    "Chat client invocation completed: {ResponseMessageCount} response message(s)",
                    responseMessageCount);

                if (activity != null)
                {
                    activity.SetTag("chat.response_message_count", responseMessageCount);
                    activity.SetStatus(ActivityStatusCode.Ok);
                }
            }
            catch (Exception ex)
            {
                exception = ex;
                logger.LogError(
                    ex,
                    "Chat client invocation failed");
                
                if (activity != null)
                {
                    activity.SetTag("chat.error", ex.Message);
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
                        if (responseSummary.Length > 500)
                        {
                            responseSummary = responseSummary[..500] + "... (truncated)";
                        }
                        activity.SetTag("chat.response_summary", responseSummary);
                    }
                }
            }

            return response!;
        };
    }
}

