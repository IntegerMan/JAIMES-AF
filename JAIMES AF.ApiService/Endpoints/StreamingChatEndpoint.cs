using System.Text;
using System.Text.Json;
using MattEland.Jaimes.ApiService.Agents;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using Microsoft.Extensions.AI;

namespace MattEland.Jaimes.ApiService.Endpoints;

/// <summary>
/// Streaming chat endpoint that properly handles Server-Sent Events (SSE) and returns message IDs immediately after persistence.
/// This replaces the MapAGUI endpoint which was buffering responses and preventing true streaming.
/// </summary>
public class StreamingChatEndpoint : Endpoint<ChatRequest, EmptyResponse>
{
    public required GameAwareAgent GameAwareAgent { get; set; }

    public override void Configure()
    {
        Console.WriteLine("!!! StreamingChatEndpoint.Configure() called - endpoint is being registered !!!");
        Post("/games/{gameId:guid}/chat");
        AllowAnonymous();
        Options(x => x.RequireHost("*")); // Accept from any host
        Description(b => b
            .Produces<EmptyResponse>(StatusCodes.Status200OK, "text/event-stream")
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Games", "Chat"));
    }

    public override async Task HandleAsync(ChatRequest req, CancellationToken ct)
    {
        Console.WriteLine($"!!! StreamingChatEndpoint.HandleAsync() called for game {req.GameId} !!!");
        Logger.LogInformation("!!! StreamingChatEndpoint.HandleAsync() called for game {GameId} !!!", req.GameId);

        // CRITICAL: Set the gameId in route values so GameAwareAgent can extract it from HttpContext
        // FastEndpoints may not populate this automatically, so we set it explicitly
        HttpContext.Request.RouteValues["gameId"] = req.GameId.ToString();

        // Set response headers for SSE
        HttpContext.Response.Headers.Append("Content-Type", "text/event-stream");
        HttpContext.Response.Headers.Append("Cache-Control", "no-cache");
        HttpContext.Response.Headers.Append("Connection", "keep-alive");
        HttpContext.Response.Headers.Append("X-Accel-Buffering", "no"); // Disable nginx buffering

        // CRITICAL: Start the response immediately so client knows we're streaming
        // Without this, the client waits forever for headers
        HttpContext.Response.StatusCode = 200;
        await HttpContext.Response.Body.FlushAsync(ct);

        // Get the stream for writing SSE events
        Stream responseStream = HttpContext.Response.Body;

        try
        {
            Logger.LogInformation("StreamingChatEndpoint: Starting chat for game {GameId}", req.GameId);

            // Convert the user's message to ChatMessage format
            List<ChatMessage> messagesToSend = new()
            {
                new ChatMessage(ChatRole.User, req.Message)
            };

            Logger.LogInformation("StreamingChatEndpoint: About to call GameAwareAgent.RunStreamingAsync");

            // Stream the agent's response
            int updateCount = 0;
            await foreach (var update in GameAwareAgent.RunStreamingAsync(messagesToSend, thread: null, options: null, ct))
            {
                updateCount++;
                Logger.LogInformation("StreamingChatEndpoint: Received update #{Count}, Role: {Role}, MessageId: {MessageId}",
                    updateCount, update.Role, update.MessageId);

                if (ct.IsCancellationRequested) break;

                // Skip user messages in the response stream
                if (update.Role == ChatRole.User) continue;

                // Check if this is a system message with persistence information
                if (update.Role == ChatRole.System && update.MessageId == "persistence-complete")
                {
                    // Send final event with database message IDs
                    await SendSseEventAsync(responseStream, "persisted", update.Text ?? "{}", ct);
                    await responseStream.FlushAsync(ct);
                    continue;
                }

                // Build the streaming update event
                var streamEvent = new ChatStreamEvent
                {
                    MessageId = update.MessageId ?? "default",
                    TextDelta = update.Text ?? string.Empty,
                    Role = update.Role?.ToString() ?? "Assistant",
                    AuthorName = update.AuthorName
                };

                // Send the SSE event
                await SendSseEventAsync(responseStream, "delta", streamEvent, ct);
                await responseStream.FlushAsync(ct);
            }

            // Send completion event
            await SendSseEventAsync(responseStream, "done", new { Message = "Stream complete" }, ct);
            await responseStream.FlushAsync(ct);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during streaming chat for game {GameId}", req.GameId);

            // Send error event
            var errorEvent = new
            {
                Error = ex.Message,
                Type = ex.GetType().Name
            };

            await SendSseEventAsync(responseStream, "error", errorEvent, ct);
            await responseStream.FlushAsync(ct);
        }
    }

    private static async Task SendSseEventAsync<T>(Stream stream, string eventType, T data, CancellationToken ct)
    {
        string json = data is string str ? str : JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        string sseMessage = $"event: {eventType}\ndata: {json}\n\n";
        byte[] bytes = Encoding.UTF8.GetBytes(sseMessage);
        await stream.WriteAsync(bytes, ct);
    }
}

/// <summary>
/// Represents a streaming delta event sent via Server-Sent Events.
/// </summary>
public record ChatStreamEvent
{
    public required string MessageId { get; init; }
    public required string TextDelta { get; init; }
    public required string Role { get; init; }
    public string? AuthorName { get; init; }
}
