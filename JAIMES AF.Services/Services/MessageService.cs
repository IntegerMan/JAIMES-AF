using MattEland.Jaimes.Domain;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.ServiceLayer.Mapping;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace MattEland.Jaimes.ServiceLayer.Services;

public class MessageService(IDbContextFactory<JaimesDbContext> contextFactory) : IMessageService
{
    public async Task<IEnumerable<MessageContextDto>> GetMessageContextAsync(int messageId,
        int countBefore,
        int countAfter,
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // First find the target message to get the game ID and ensure it exists
        Message? targetMessage = await context.Messages
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);

        if (targetMessage == null)
        {
            throw new ArgumentException($"Message {messageId} not found", nameof(messageId));
        }

        // Get messages before and up to the target message (context leading up to it)
        List<Message> messagesBefore = await context.Messages
            .AsNoTracking()
            .Where(m => m.GameId == targetMessage.GameId && m.Id <= messageId)
            .OrderByDescending(m => m.Id)
            .Take(countBefore)
            .Include(m => m.Player)
            .Include(m => m.ChatHistory)
            .Include(m => m.ToolCalls)
            .Include(m => m.MessageSentiment)
            .Include(m => m.InstructionVersion)
            .Include(m => m.Agent)
            .Include(m => m.TestCase)
            .ToListAsync(cancellationToken);

        // Reverse to chronological order (oldest first)
        messagesBefore.Reverse();
        var allMessages = messagesBefore;

        if (countAfter > 0)
        {
            // Also get messages after the target message (to capture the assistant response with feedback)
            List<Message> messagesAfter = await context.Messages
                .AsNoTracking()
                .Where(m => m.GameId == targetMessage.GameId && m.Id > messageId)
                .OrderBy(m => m.Id)
                .Take(countAfter)
                .Include(m => m.Player)
                .Include(m => m.ChatHistory)
                .Include(m => m.ToolCalls)
                .Include(m => m.MessageSentiment)
                .Include(m => m.InstructionVersion)
                .Include(m => m.Agent)
                .Include(m => m.TestCase)
                .ToListAsync(cancellationToken);

            allMessages = allMessages.Concat(messagesAfter).ToList();
        }

        // Fetch registered evaluators once to check for missing ones
        var registeredEvaluatorIds = await context.Evaluators
            .AsNoTracking()
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);
        int totalEvaluatorCount = registeredEvaluatorIds.Count;

        // Fetch metrics and feedback manually since navigation properties aren't configured
        var messageIds = allMessages.Select(m => m.Id).ToList();

        var metrics = await context.MessageEvaluationMetrics
            .AsNoTracking()
            .Where(m => messageIds.Contains(m.MessageId))
            .ToListAsync(cancellationToken);

        var feedbacks = await context.MessageFeedbacks
            .AsNoTracking()
            .Where(f => messageIds.Contains(f.MessageId))
            .ToListAsync(cancellationToken);

        var dtos = new List<MessageContextDto>();
        foreach (var message in allMessages)
        {
            var dto = message.ToContextDto();
            dto.InstructionVersionNumber = message.InstructionVersion?.VersionNumber;
            dto.Metrics = metrics
                .Where(m => m.MessageId == message.Id)
                .Select(MessageMapper.ToResponse)
                .ToList();

            var feedback = feedbacks.FirstOrDefault(f => f.MessageId == message.Id);
            if (feedback != null)
            {
                dto.Feedback = MessageMapper.ToResponse(feedback);
            }


            if (!message.IsScriptedMessage && message.PlayerId == null) // AI message
            {
                var msgMetrics = metrics.Where(m => m.MessageId == message.Id).ToList();
                int msgEvaluatorCount = msgMetrics
                    .Where(m => m.EvaluatorId.HasValue)
                    .Select(m => m.EvaluatorId!.Value)
                    .Distinct()
                    .Count();

                dto.HasMissingEvaluators = msgEvaluatorCount < totalEvaluatorCount;
            }

            dtos.Add(dto);
        }

        return dtos;
    }

    public async Task<IEnumerable<MessageContextDto>> GetMessagesByAgentAsync(string? agentId,
        int? versionId,
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var query = context.Messages
            .AsNoTracking()
            .Where(m => !m.IsScriptedMessage);

        if (!string.IsNullOrEmpty(agentId))
        {
            string agentIdLower = agentId.ToLower();
            query = query.Where(m => m.AgentId.ToLower() == agentIdLower);
        }

        if (versionId.HasValue)
        {
            query = query.Where(m => m.InstructionVersionId == versionId.Value);
        }

        List<Message> messages = await query
            .OrderBy(m => m.GameId)
            .ThenBy(m => m.CreatedAt)
            .Include(m => m.Player)
            .Include(m => m.Game) // Include Game for title
            .Include(m => m.ToolCalls)
            .Include(m => m.MessageSentiment)
            .Include(m => m.InstructionVersion)
            .ToListAsync(cancellationToken);

        // Fetch metrics and feedback
        var messageIds = messages.Select(m => m.Id).ToList();

        var metrics = await context.MessageEvaluationMetrics
            .AsNoTracking()
            .Where(m => messageIds.Contains(m.MessageId))
            .ToListAsync(cancellationToken);

        var feedbacks = await context.MessageFeedbacks
            .AsNoTracking()
            .Where(f => messageIds.Contains(f.MessageId))
            .ToListAsync(cancellationToken);

        // Fetch registered evaluators once to check for missing ones
        var registeredEvaluatorIds = await context.Evaluators
            .AsNoTracking()
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);
        int totalEvaluatorCount = registeredEvaluatorIds.Count;

        var dtos = new List<MessageContextDto>();
        foreach (var message in messages)
        {
            var dto = message.ToContextDto();
            dto.GameTitle = message.Game?.Title;
            dto.InstructionVersionNumber = message.InstructionVersion?.VersionNumber;

            dto.Metrics = metrics
                .Where(m => m.MessageId == message.Id)
                .Select(MessageMapper.ToResponse)
                .ToList();

            var feedback = feedbacks.FirstOrDefault(f => f.MessageId == message.Id);
            if (feedback != null)
            {
                dto.Feedback = MessageMapper.ToResponse(feedback);
            }

            if (!message.IsScriptedMessage && message.PlayerId == null) // AI message
            {
                int msgEvaluatorCount = dto.Metrics
                    .Where(m => m.EvaluatorId.HasValue)
                    .Select(m => m.EvaluatorId!.Value)
                    .Distinct()
                    .Count();

                dto.HasMissingEvaluators = msgEvaluatorCount < totalEvaluatorCount;
            }

            dtos.Add(dto);
        }

        return dtos;
    }

    public async Task<IEnumerable<JsonlExportRecord>> GetJsonlExportDataAsync(string agentId,
        int versionId,
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Retrieve all messages for this agent version with related data
        var messages = await GetMessagesByAgentAsync(agentId, versionId, cancellationToken);

        // Also need to load TestCase data which isn't included in GetMessagesByAgentAsync
        var messageIds = messages.Select(m => m.Id).ToList();
        var testCases = await context.TestCases
            .AsNoTracking()
            .Where(tc => messageIds.Contains(tc.MessageId))
            .ToListAsync(cancellationToken);

        var exportRecords = new List<JsonlExportRecord>();

        // Group messages by GameId to maintain conversation context
        var messagesByGame = messages.GroupBy(m => m.GameId);

        foreach (var gameMessages in messagesByGame)
        {
            // Order messages chronologically within each game
            var orderedMessages = gameMessages.OrderBy(m => m.CreatedAt).ToList();

            MessageContextDto? pendingUserMessage = null;

            foreach (var message in orderedMessages)
            {
                // Check if this is a user message (PlayerId is not null)
                if (message.PlayerId != null)
                {
                    pendingUserMessage = message;
                }
                // Check if this is an agent response (PlayerId is null) and we have a pending user message
                else if (pendingUserMessage != null)
                {
                    // Extract ground truth from TestCase if available
                    string? groundTruth = null;
                    var testCase = testCases.FirstOrDefault(tc => tc.MessageId == pendingUserMessage.Id);
                    if (testCase != null && !string.IsNullOrWhiteSpace(testCase.Description))
                    {
                        groundTruth = testCase.Description;
                    }

                    // Extract context from tool calls
                    string? extractedContext = ExtractContextFromToolCalls(message.ToolCalls);

                    // Create export record
                    var record = new JsonlExportRecord
                    {
                        Query = pendingUserMessage.Text,
                        GroundTruth = groundTruth,
                        Response = message.Text,
                        Context = extractedContext
                    };

                    exportRecords.Add(record);

                    // Clear pending user message after pairing
                    pendingUserMessage = null;
                }
            }
        }

        return exportRecords;
    }

    /// <summary>
    /// Extracts context information from message tool calls, particularly from RAG/search results.
    /// </summary>
    private static string? ExtractContextFromToolCalls(List<MessageToolCallResponse> toolCalls)
    {
        if (toolCalls == null || toolCalls.Count == 0)
        {
            return null;
        }

        var contextParts = toolCalls
            .Where(tc => !string.IsNullOrWhiteSpace(tc.OutputJson))
            .SelectMany(tc => ParseToolCallContext(tc.OutputJson!))
            .ToList();

        return contextParts.Count > 0 ? string.Join("\n\n", contextParts) : null;
    }

    /// <summary>
    /// Parses a single tool call's output JSON to extract context information.
    /// </summary>
    private static IEnumerable<string> ParseToolCallContext(string outputJson)
    {
        JsonDocument? doc = null;
        try
        {
            doc = JsonDocument.Parse(outputJson);
            JsonElement root = doc.RootElement;

            // Check if this looks like a search response with Results array
            if (!root.TryGetProperty("Results", out JsonElement resultsElement) ||
                resultsElement.ValueKind != JsonValueKind.Array)
            {
                return Enumerable.Empty<string>();
            }

            var contextItems = new List<string>();
            foreach (JsonElement result in resultsElement.EnumerateArray())
            {
                var contextItem = ExtractContextItem(result);
                if (contextItem != null)
                {
                    contextItems.Add(contextItem);
                }
            }

            return contextItems;
        }
        catch (JsonException)
        {
            // If JSON parsing fails, return empty (skip this tool call)
            // This is expected for tool calls that don't contain structured JSON
            return Enumerable.Empty<string>();
        }
        finally
        {
            doc?.Dispose();
        }
    }

    /// <summary>
    /// Extracts a single context item from a search result JSON element.
    /// </summary>
    private static string? ExtractContextItem(JsonElement result)
    {
        string? documentName = result.TryGetProperty("DocumentName", out JsonElement docNameElement)
            ? docNameElement.GetString()
            : null;

        string? text = result.TryGetProperty("Text", out JsonElement textElement)
            ? textElement.GetString()
            : null;

        if (!string.IsNullOrWhiteSpace(documentName) && !string.IsNullOrWhiteSpace(text))
        {
            return $"{documentName}: {text}";
        }

        return null;
    }
}
