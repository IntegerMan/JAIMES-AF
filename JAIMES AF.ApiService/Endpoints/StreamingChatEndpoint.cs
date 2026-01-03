using System.Text;
using System.Text.Json;
using MattEland.Jaimes.ApiService.Agents;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using Microsoft.Extensions.AI;

namespace MattEland.Jaimes.ApiService.Endpoints;

/// <summary>
/// Streaming chat endpoint that handles Server-Sent Events (SSE) and returns message IDs immediately after persistence.
/// </summary>
public class StreamingChatEndpoint : Endpoint<ChatRequest, EmptyResponse>
{
    public required GameAwareAgent GameAwareAgent { get; set; }

    public override void Configure()
    {
        Post("/games/{gameId:guid}/chat");
        AllowAnonymous();
        Description(b => b
            .Produces<EmptyResponse>(StatusCodes.Status200OK, "text/event-stream")
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Games", "Chat"));
    }

    public override async Task HandleAsync(ChatRequest req, CancellationToken ct)
    {
        HttpContext.Request.RouteValues["gameId"] = req.GameId.ToString();
        ConfigureSseResponse();
        await HttpContext.Response.Body.FlushAsync(ct);

        Stream responseStream = HttpContext.Response.Body;

        try
        {
            await StreamChatResponseAsync(responseStream, req.Message, ct);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during streaming chat for game {GameId}", req.GameId);
            await SendSseEventAsync(responseStream, "error", new { Error = ex.Message, Type = ex.GetType().Name }, ct);
            await responseStream.FlushAsync(ct);
        }
    }

    private void ConfigureSseResponse()
    {
        HttpContext.Response.Headers.Append("Content-Type", "text/event-stream");
        HttpContext.Response.Headers.Append("Cache-Control", "no-cache");
        HttpContext.Response.Headers.Append("Connection", "keep-alive");
        HttpContext.Response.Headers.Append("X-Accel-Buffering", "no");
        HttpContext.Response.StatusCode = 200;
    }

    private async Task StreamChatResponseAsync(Stream responseStream, string message, CancellationToken ct)
    {
        List<ChatMessage> messagesToSend = new() { new ChatMessage(ChatRole.User, message) };

        await foreach (var update in GameAwareAgent.RunStreamingAsync(messagesToSend, thread: null, options: null, ct))
        {
            if (ct.IsCancellationRequested) break;
            if (update.Role == ChatRole.User) continue;

            if (update.Role == ChatRole.System && update.MessageId == "persistence-complete")
            {
                await SendSseEventAsync(responseStream, "persisted", update.Text ?? "{}", ct);
                await responseStream.FlushAsync(ct);
                continue;
            }

            var streamEvent = new ChatStreamEvent
            {
                MessageId = update.MessageId ?? "default",
                TextDelta = update.Text ?? string.Empty,
                Role = update.Role?.ToString() ?? "Assistant",
                AuthorName = update.AuthorName
            };

            await SendSseEventAsync(responseStream, "delta", streamEvent, ct);
            await responseStream.FlushAsync(ct);
        }

        await SendSseEventAsync(responseStream, "done", new { Message = "Stream complete" }, ct);
        await responseStream.FlushAsync(ct);
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
