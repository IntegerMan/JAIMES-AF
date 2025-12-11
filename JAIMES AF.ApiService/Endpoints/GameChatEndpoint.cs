using System.Text;
using System.Text.Json;
using MattEland.Jaimes.Agents.Helpers;
using MattEland.Jaimes.Tools;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class GameChatEndpoint : EndpointWithoutRequest
{
    public required IGameService GameService { get; set; }
    public required IChatHistoryService ChatHistoryService { get; set; }
    public required IChatClient ChatClient { get; set; }
    public required ILogger<GameChatEndpoint> EndpointLogger { get; set; }
    public required IRulesSearchService? RulesSearchService { get; set; }
    public required IDbContextFactory<JaimesDbContext> ContextFactory { get; set; }

    public override void Configure()
    {
        Post("/games/{gameId:guid}/chat");
        AllowAnonymous();
        Description(b => b
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Games"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        string? gameIdStr = Route<string>("gameId", true);
        if (!Guid.TryParse(gameIdStr, out Guid gameId))
        {
            ThrowError("Invalid game ID format");
            return;
        }

        // Get the game
        GameDto? gameDto = await GameService.GetGameAsync(gameId, ct);
        if (gameDto == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        // Read the request body (AG-UI format)
        string requestBody;
        using (StreamReader reader = new(HttpContext.Request.Body, Encoding.UTF8))
        {
            requestBody = await reader.ReadToEndAsync(ct);
        }

        if (string.IsNullOrWhiteSpace(requestBody))
        {
            ThrowError("Request body is required");
            return;
        }

        // Parse AG-UI request
        JsonElement requestJson = JsonSerializer.Deserialize<JsonElement>(requestBody, JsonSerializerOptions.Web);
        
        // Extract messages from request
        List<ChatMessage> messages = [];
        if (requestJson.TryGetProperty("messages", out JsonElement messagesElement) && messagesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement messageElement in messagesElement.EnumerateArray())
            {
                if (messageElement.TryGetProperty("role", out JsonElement roleElement) &&
                    messageElement.TryGetProperty("content", out JsonElement contentElement))
                {
                    string roleStr = roleElement.GetString() ?? "user";
                    ChatRole role = roleStr.Equals("assistant", StringComparison.OrdinalIgnoreCase) 
                        ? ChatRole.Assistant 
                        : ChatRole.User;
                    string content = contentElement.GetString() ?? string.Empty;
                    messages.Add(new ChatMessage(role, content));
                }
            }
        }

        if (messages.Count == 0)
        {
            ThrowError("At least one message is required");
            return;
        }

        // Create agent with game context
        IChatClient instrumentedChatClient = ChatClient.WrapWithInstrumentation(EndpointLogger);
        AIAgent agent = instrumentedChatClient.CreateJaimesAgent(
            EndpointLogger, 
            $"JaimesAgent-{gameId}", 
            gameDto.Scenario.SystemPrompt, 
            CreateTools(gameDto));

        // Load or create thread
        AgentThread? thread = null;
        string? existingThreadJson = await ChatHistoryService.GetMostRecentThreadJsonAsync(gameId, ct);
        if (!string.IsNullOrEmpty(existingThreadJson))
        {
            JsonElement jsonElement = JsonSerializer.Deserialize<JsonElement>(existingThreadJson, JsonSerializerOptions.Web);
            thread = agent.DeserializeThread(jsonElement, JsonSerializerOptions.Web);
        }

        thread ??= agent.GetNewThread();

        try
        {
            // Extract the new user message (last message with role "user")
            ChatMessage? newUserMessage = messages.LastOrDefault(m => m.Role == ChatRole.User);
            if (newUserMessage == null)
            {
                ThrowError("At least one user message is required");
                return;
            }

            // Run agent with thread
            AgentRunResponse response = await agent.RunAsync(messages, thread, cancellationToken: ct);

            // Persist messages to database
            await using JaimesDbContext context = await ContextFactory.CreateDbContextAsync(ct);
            
            // Save user message
            Message userMessageEntity = new()
            {
                GameId = gameId,
                Text = newUserMessage.Text ?? string.Empty,
                PlayerId = gameDto.Player.Id,
                CreatedAt = DateTime.UtcNow
            };
            context.Messages.Add(userMessageEntity);

            // Save AI messages
            List<Message> aiMessageEntities = response.Messages
                .Select(m => new Message
                {
                    GameId = gameId,
                    Text = m.Text ?? string.Empty,
                    PlayerId = null,
                    CreatedAt = DateTime.UtcNow
                })
                .ToList();
            context.Messages.AddRange(aiMessageEntities);

            await context.SaveChangesAsync(ct);

            // Get the last AI message ID for thread association
            int? lastAiMessageId = aiMessageEntities.LastOrDefault()?.Id;

            // Serialize and save thread after completion
            string threadJson = thread.Serialize(JsonSerializerOptions.Web).GetRawText();
            await ChatHistoryService.SaveThreadJsonAsync(gameId, threadJson, lastAiMessageId, ct);

            // Set up SSE response
            HttpContext.Response.ContentType = "text/event-stream";
            HttpContext.Response.Headers.CacheControl = "no-cache";
            HttpContext.Response.Headers.Connection = "keep-alive";

            // Format response as AG-UI SSE
            foreach (ChatMessage message in response.Messages)
            {
                string sseData = FormatAsSSE(message);
                byte[] data = Encoding.UTF8.GetBytes(sseData);
                await HttpContext.Response.Body.WriteAsync(data, ct);
                await HttpContext.Response.Body.FlushAsync(ct);
            }

            // Send completion event
            string doneData = "data: [DONE]\n\n";
            byte[] doneBytes = Encoding.UTF8.GetBytes(doneData);
            await HttpContext.Response.Body.WriteAsync(doneBytes, ct);
            await HttpContext.Response.Body.FlushAsync(ct);
        }
        catch (Exception ex)
        {
            EndpointLogger.LogError(ex, "Error processing chat message for game {GameId}", gameId);
            HttpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            string errorData = $"data: {JsonSerializer.Serialize(new { error = ex.Message })}\n\n";
            byte[] errorBytes = Encoding.UTF8.GetBytes(errorData);
            await HttpContext.Response.Body.WriteAsync(errorBytes, ct);
            await HttpContext.Response.Body.FlushAsync(ct);
        }
    }

    private IList<AITool>? CreateTools(GameDto game)
    {
        List<AITool> toolList = [];

        PlayerInfoTool playerInfoTool = new(game);
        AIFunction playerInfoFunction = AIFunctionFactory.Create(
            () => playerInfoTool.GetPlayerInfo(),
            "GetPlayerInfo",
            "Retrieves detailed information about the current player character in the game, including their name, unique identifier, and character description. Use this tool whenever you need to reference or describe the player character, their background, or their current state in the game world.");
        toolList.Add(playerInfoFunction);

        if (RulesSearchService != null)
        {
            RulesSearchTool rulesSearchTool = new(game, RulesSearchService);
            AIFunction rulesSearchFunction = AIFunctionFactory.Create(
                (string query) => rulesSearchTool.SearchRulesAsync(query),
                "SearchRules",
                "Searches the ruleset's indexed rules to find answers to specific questions or queries. This is a rules search tool that gets answers from rules to specific questions or queries. Use this tool whenever you need to look up game rules, mechanics, or rule clarifications. The tool will search through the indexed rules for the current scenario's ruleset and return relevant information.");
            toolList.Add(rulesSearchFunction);
        }

        return toolList;
    }

    private static string FormatAsSSE(ChatMessage message)
    {
        // Format as Server-Sent Events for AG-UI protocol
        var data = new
        {
            role = message.Role == ChatRole.Assistant ? "assistant" : "user",
            content = message.Text ?? string.Empty
        };
        return $"data: {JsonSerializer.Serialize(data)}\n\n";
    }
}

