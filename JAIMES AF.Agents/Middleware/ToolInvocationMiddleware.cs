using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MattEland.Jaimes.Agents.Middleware;

/// <summary>
/// Middleware that tracks tool invocations and logs them with OpenTelemetry.
/// This middleware intercepts function calls to track which tools were considered and invoked.
/// </summary>
public static class ToolInvocationMiddleware
{
    private static readonly ActivitySource ActivitySource = new("Jaimes.Agents.Tools");

    /// <summary>
    /// Creates a function calling middleware that tracks tool invocations.
    /// </summary>
    /// <param name="logger">The logger to use for logging tool invocations.</param>
    /// <param name="getServiceProvider">A function that returns the current request's service provider when called.</param>
    /// <returns>A middleware function that can be used with the agent builder.</returns>
    public static
        Func<AIAgent, FunctionInvocationContext, Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>>,
            CancellationToken, ValueTask<object?>> Create(ILogger logger, Func<IServiceProvider?>? getServiceProvider = null)
    {
        // Log that middleware is being created/registered
        logger.LogInformation("ToolInvocationMiddleware created and registered");

        return async (agent, context, next, cancellationToken) =>
        {
            string functionName = context.Function?.Name ?? "unknown";
            string? functionDescription = context.Function?.Description;

            // Log that a tool is being invoked - this confirms the middleware is working
            logger.LogInformation(
                "🔧 Tool invocation started: {FunctionName} (Description: {FunctionDescription})",
                functionName,
                functionDescription);

            // Also log context details for debugging
            logger.LogDebug(
                "Tool invocation context - Function: {FunctionName}, Arguments: {Arguments}",
                functionName,
                context.Arguments != null ? JsonSerializer.Serialize(context.Arguments) : "null");

            // Create an OpenTelemetry activity for the tool invocation
            using Activity? activity = ActivitySource.StartActivity($"Tool.Invoke.{functionName}");

            if (activity != null)
            {
                activity.SetTag("tool.name", functionName);
                activity.SetTag("tool.description", functionDescription ?? string.Empty);

                // Log function arguments if available
                if (context.Arguments != null)
                    activity.SetTag("tool.arguments", JsonSerializer.Serialize(context.Arguments));
            }

            object? result = null;
            Exception? exception = null;

            try
            {
                // Execute the actual function call
                result = await next(context, cancellationToken);

                // Log successful invocation
                logger.LogInformation(
                    "Tool invocation completed: {FunctionName} (Result: {ResultType})",
                    functionName,
                    result?.GetType().Name ?? "null");

                if (activity != null)
                {
                    activity.SetTag("tool.result_type", result?.GetType().Name ?? "null");
                    activity.SetStatus(ActivityStatusCode.Ok);
                }

                // Record tool call in tracker if available
                // Get service provider from delegate to avoid disposed scope issues
                if (getServiceProvider != null)
                {
                    try
                    {
                        IServiceProvider? requestServices = getServiceProvider();
                        if (requestServices != null)
                        {
                            IToolCallTracker? tracker = requestServices.GetService<IToolCallTracker>();
                            if (tracker != null)
                            {
                                logger.LogInformation("Recording tool call '{ToolName}' in tracker", functionName);
                                await tracker.RecordToolCallAsync(functionName, context.Arguments, result);
                                logger.LogInformation("Successfully recorded tool call '{ToolName}' in tracker", functionName);
                            }
                            else
                            {
                                logger.LogWarning("IToolCallTracker not found in service provider for tool '{ToolName}'", functionName);
                            }
                        }
                        else
                        {
                            logger.LogWarning("Service provider is null, cannot record tool call '{ToolName}'", functionName);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log but don't fail the tool call if tracking fails
                        logger.LogWarning(ex, "Failed to record tool call in tracker: {FunctionName}", functionName);
                    }
                }
                else
                {
                    logger.LogDebug("getServiceProvider is null, skipping tool call tracking for '{ToolName}'", functionName);
                }
            }
            catch (Exception ex)
            {
                exception = ex;
                logger.LogError(
                    ex,
                    "Tool invocation failed: {FunctionName}",
                    functionName);

                if (activity != null)
                {
                    activity.SetTag("tool.error", ex.Message);
                    activity.SetStatus(ActivityStatusCode.Error, ex.Message);
                }

                throw;
            }
            finally
            {
                if (activity != null && exception == null)
                    // Log result summary if available
                    if (result != null)
                    {
                        string resultSummary = result.ToString() ?? "null";
                        // Truncate long results for logging
                        if (resultSummary.Length > 500) resultSummary = resultSummary[..500] + "... (truncated)";
                        activity.SetTag("tool.result_summary", resultSummary);
                    }
            }

            return result;
        };
    }
}