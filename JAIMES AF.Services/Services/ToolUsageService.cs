using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.EntityFrameworkCore;

namespace MattEland.Jaimes.ServiceLayer.Services;

/// <summary>
/// Service for retrieving tool usage statistics.
/// </summary>
public class ToolUsageService(
    IDbContextFactory<JaimesDbContext> contextFactory,
    IToolRegistry toolRegistry,
    IMessageService messageService) : IToolUsageService
{
    /// <inheritdoc />
    public async Task<ToolUsageListResponse> GetToolUsageAsync(
        int page,
        int pageSize,
        string? agentId = null,
        int? instructionVersionId = null,
        Guid? gameId = null,
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Build the base query for tool calls with optional filters
        IQueryable<MessageToolCall> toolCallQuery = context.MessageToolCalls
            .AsNoTracking()
            .Include(mtc => mtc.Message)
            .Include(mtc => mtc.InstructionVersion)
            .ThenInclude(iv => iv!.Agent);

        // Apply optional filters
        if (instructionVersionId.HasValue)
        {
            toolCallQuery = toolCallQuery.Where(mtc => mtc.InstructionVersionId == instructionVersionId.Value);
        }
        else if (!string.IsNullOrEmpty(agentId))
        {
            toolCallQuery = toolCallQuery.Where(mtc =>
                mtc.InstructionVersion != null && mtc.InstructionVersion.AgentId == agentId);
        }

        if (gameId.HasValue)
        {
            toolCallQuery = toolCallQuery.Where(mtc => mtc.Message != null && mtc.Message.GameId == gameId.Value);
        }

        // Get all tool calls to process in memory for grouping
        List<MessageToolCall> allToolCalls = await toolCallQuery.ToListAsync(cancellationToken);

        // Get message IDs that have tool calls for feedback lookup
        HashSet<int> messageIdsWithToolCalls = allToolCalls
            .Select(mtc => mtc.MessageId)
            .ToHashSet();

        // Get feedback for messages that have tool calls
        Dictionary<int, bool> feedbackByMessageId = await context.MessageFeedbacks
            .AsNoTracking()
            .Where(mf => messageIdsWithToolCalls.Contains(mf.MessageId))
            .ToDictionaryAsync(mf => mf.MessageId, mf => mf.IsPositive, cancellationToken);

        // Group by tool name to get statistics for tools that have been called
        var toolCallStats = allToolCalls
            .GroupBy(mtc => mtc.ToolName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                grp => grp.Key,
                grp =>
                {
                    var toolCallsForTool = grp.ToList();
                    var messageIds = toolCallsForTool.Select(mtc => mtc.MessageId).Distinct().ToList();

                    int helpfulCount = messageIds.Count(mid =>
                        feedbackByMessageId.TryGetValue(mid, out bool isPositive) && isPositive);
                    int unhelpfulCount = messageIds.Count(mid =>
                        feedbackByMessageId.TryGetValue(mid, out bool isPositive) && !isPositive);

                    return (
                        TotalCalls: grp.Count(),
                        MessageCount: messageIds.Count,
                        EnabledAgents: grp
                            .Where(mtc => mtc.InstructionVersion?.Agent != null)
                            .Select(mtc =>
                                $"{mtc.InstructionVersion!.Agent!.Name} v{mtc.InstructionVersion.VersionNumber}")
                            .Distinct()
                            .ToList(),
                        HelpfulCount: helpfulCount,
                        UnhelpfulCount: unhelpfulCount
                    );
                }, StringComparer.OrdinalIgnoreCase);

        // Calculate eligible messages count
        // Assistant messages are those where PlayerId is null
        IQueryable<Message> eligibleMessagesQuery = context.Messages
            .AsNoTracking()
            .Where(m => m.PlayerId == null);

        // Apply the same filters to eligible messages
        if (instructionVersionId.HasValue)
        {
            eligibleMessagesQuery =
                eligibleMessagesQuery.Where(m => m.InstructionVersionId == instructionVersionId.Value);
        }
        else if (!string.IsNullOrEmpty(agentId))
        {
            eligibleMessagesQuery = eligibleMessagesQuery.Where(m =>
                m.InstructionVersion != null && m.InstructionVersion.AgentId == agentId);
        }

        if (gameId.HasValue)
        {
            eligibleMessagesQuery = eligibleMessagesQuery.Where(m => m.GameId == gameId.Value);
        }

        int eligibleMessagesCount = await eligibleMessagesQuery.CountAsync(cancellationToken);

        // Get all registered tools from the registry
        IReadOnlyList<ToolMetadata> registeredTools = toolRegistry.GetAllTools();

        // Create a list of all tools, including those without calls
        List<ToolUsageItemDto> allToolItems = registeredTools
            .Select(tool =>
            {
                bool hasStats = toolCallStats.TryGetValue(tool.Name, out var stats);
                int totalCalls = hasStats ? stats.TotalCalls : 0;
                List<string> enabledAgents = hasStats ? stats.EnabledAgents : [];
                int helpfulCount = hasStats ? stats.HelpfulCount : 0;
                int unhelpfulCount = hasStats ? stats.UnhelpfulCount : 0;

                return new ToolUsageItemDto
                {
                    ToolName = tool.Name,
                    TotalCalls = totalCalls,
                    EligibleMessages = eligibleMessagesCount,
                    UsagePercentage = eligibleMessagesCount > 0 && hasStats
                        ? Math.Clamp(Math.Round((double)stats.MessageCount / eligibleMessagesCount * 100, 2), 0, 100)
                        : 0,
                    EnabledAgents = enabledAgents,
                    HelpfulCount = helpfulCount,
                    UnhelpfulCount = unhelpfulCount
                };
            })
            .OrderByDescending(x => x.TotalCalls)
            .ThenBy(x => x.ToolName)
            .ToList();

        // Include any tools that were called but aren't in the registry (for backwards compatibility)
        var unregisteredTools = toolCallStats.Keys
            .Where(name => !registeredTools.Any(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)))
            .Select(name =>
            {
                var stats = toolCallStats[name];
                return new ToolUsageItemDto
                {
                    ToolName = name,
                    TotalCalls = stats.TotalCalls,
                    EligibleMessages = eligibleMessagesCount,
                    UsagePercentage = eligibleMessagesCount > 0
                        ? Math.Clamp(Math.Round((double)stats.MessageCount / eligibleMessagesCount * 100, 2), 0, 100)
                        : 0,
                    EnabledAgents = stats.EnabledAgents,
                    HelpfulCount = stats.HelpfulCount,
                    UnhelpfulCount = stats.UnhelpfulCount
                };
            });

        allToolItems.AddRange(unregisteredTools);
        allToolItems = allToolItems.OrderByDescending(x => x.TotalCalls).ThenBy(x => x.ToolName).ToList();

        int totalCount = allToolItems.Count;

        // Apply pagination
        List<ToolUsageItemDto> items = allToolItems
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new ToolUsageListResponse
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<ToolCallDetailListResponse> GetToolCallDetailsAsync(
        string toolName,
        int page,
        int pageSize,
        string? agentId = null,
        int? instructionVersionId = null,
        Guid? gameId = null,
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Build query for tool calls for the specific tool
        IQueryable<MessageToolCall> query = context.MessageToolCalls
            .AsNoTracking()
            .Include(mtc => mtc.Message)
            .ThenInclude(m => m!.Game)
            .ThenInclude(g => g!.Scenario)
            .Include(mtc => mtc.Message)
            .ThenInclude(m => m!.Game)
            .ThenInclude(g => g!.Player)
            .Include(mtc => mtc.InstructionVersion)
            .ThenInclude(iv => iv!.Agent)
            .Where(mtc => mtc.ToolName.ToLower() == toolName.ToLower());

        // Apply optional filters
        if (instructionVersionId.HasValue)
        {
            query = query.Where(mtc => mtc.InstructionVersionId == instructionVersionId.Value);
        }
        else if (!string.IsNullOrEmpty(agentId))
        {
            query = query.Where(mtc => mtc.InstructionVersion != null && mtc.InstructionVersion.AgentId == agentId);
        }

        if (gameId.HasValue)
        {
            query = query.Where(mtc => mtc.Message != null && mtc.Message.GameId == gameId.Value);
        }

        int totalCount = await query.CountAsync(cancellationToken);

        // Get paginated results
        List<MessageToolCall> toolCalls = await query
            .OrderByDescending(mtc => mtc.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // Get feedback for these messages
        HashSet<int> messageIds = toolCalls.Select(mtc => mtc.MessageId).ToHashSet();
        Dictionary<int, MessageFeedback> feedbackByMessageId = await context.MessageFeedbacks
            .AsNoTracking()
            .Where(mf => messageIds.Contains(mf.MessageId))
            .ToDictionaryAsync(mf => mf.MessageId, mf => mf, cancellationToken);

        // Map to DTOs
        List<ToolCallDetailDto> items = toolCalls.Select(mtc =>
        {
            feedbackByMessageId.TryGetValue(mtc.MessageId, out MessageFeedback? feedback);

            string? gameName = null;
            if (mtc.Message?.Game != null)
            {
                gameName =
                    $"{mtc.Message.Game.Scenario?.Name ?? "Unknown Scenario"} - {mtc.Message.Game.Player?.Name ?? "Unknown Player"}";
            }

            return new ToolCallDetailDto
            {
                Id = mtc.Id,
                ToolName = mtc.ToolName,
                CreatedAt = mtc.CreatedAt,
                MessageId = mtc.MessageId,
                GameId = mtc.Message?.GameId,
                GameName = gameName,
                AgentName = mtc.InstructionVersion?.Agent?.Name,
                AgentVersion = mtc.InstructionVersion?.VersionNumber,
                FeedbackIsPositive = feedback?.IsPositive,
                FeedbackComment = feedback?.Comment
            };
        }).ToList();

        // Get tool metadata from registry
        ToolMetadata? toolMetadata = toolRegistry.GetTool(toolName);

        return new ToolCallDetailListResponse
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            ToolName = toolName,
            ToolDescription = toolMetadata?.Description
        };
    }

    /// <inheritdoc />
    public async Task<ToolCallFullDetailResponse?> GetToolCallFullDetailsAsync(int toolCallId,
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Fetch the tool call
        MessageToolCall? toolCall = await context.MessageToolCalls
            .AsNoTracking()
            .Include(mtc => mtc.Message)
            .ThenInclude(m => m!.Game)
            .ThenInclude(g => g!.Scenario)
            .Include(mtc => mtc.Message)
            .ThenInclude(m => m!.Game)
            .ThenInclude(g => g!.Player)
            .Include(mtc => mtc.InstructionVersion)
            .ThenInclude(iv => iv!.Agent)
            .FirstOrDefaultAsync(mtc => mtc.Id == toolCallId, cancellationToken);

        if (toolCall == null || toolCall.Message == null)
        {
            return null;
        }

        // Get feedback
        MessageFeedback? feedback = await context.MessageFeedbacks
            .AsNoTracking()
            .FirstOrDefaultAsync(mf => mf.MessageId == toolCall.MessageId, cancellationToken);

        string? gameName = null;
        if (toolCall.Message.Game != null)
        {
            gameName =
                $"{toolCall.Message.Game.Scenario?.Name ?? "Unknown Scenario"} - {toolCall.Message.Game.Player?.Name ?? "Unknown Player"}";
        }

        // Get context messages (last 5 messages leading up to and including the tool call message)
        IEnumerable<MessageContextDto> contextMessages =
            await messageService.GetMessageContextAsync(toolCall.MessageId, 5, cancellationToken);

        return new ToolCallFullDetailResponse
        {
            Id = toolCall.Id,
            ToolName = toolCall.ToolName,
            CreatedAt = toolCall.CreatedAt,
            MessageId = toolCall.MessageId,
            GameId = toolCall.Message.GameId,
            GameName = gameName,
            AgentName = toolCall.InstructionVersion?.Agent?.Name,
            AgentVersion = toolCall.InstructionVersion?.VersionNumber,
            FeedbackIsPositive = feedback?.IsPositive,
            FeedbackComment = feedback?.Comment,
            InputJson = toolCall.InputJson,
            OutputJson = toolCall.OutputJson,
            ContextMessages = contextMessages.ToList()
        };
    }
}

