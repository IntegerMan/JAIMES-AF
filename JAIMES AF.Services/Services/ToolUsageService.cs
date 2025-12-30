using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.EntityFrameworkCore;

namespace MattEland.Jaimes.ServiceLayer.Services;

/// <summary>
/// Service for retrieving tool usage statistics.
/// </summary>
public class ToolUsageService(IDbContextFactory<JaimesDbContext> contextFactory) : IToolUsageService
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
            toolCallQuery = toolCallQuery.Where(mtc => mtc.InstructionVersion != null && mtc.InstructionVersion.AgentId == agentId);
        }

        if (gameId.HasValue)
        {
            toolCallQuery = toolCallQuery.Where(mtc => mtc.Message != null && mtc.Message.GameId == gameId.Value);
        }

        // Get all tool calls to process in memory for grouping
        List<MessageToolCall> allToolCalls = await toolCallQuery.ToListAsync(cancellationToken);

        // Group by tool name to get statistics
        var toolGroups = allToolCalls
            .GroupBy(mtc => mtc.ToolName)
            .Select(grp => new
            {
                ToolName = grp.Key,
                TotalCalls = grp.Count(),
                EnabledAgents = grp
                    .Where(mtc => mtc.InstructionVersion?.Agent != null)
                    .Select(mtc => $"{mtc.InstructionVersion!.Agent!.Name} v{mtc.InstructionVersion.VersionNumber}")
                    .Distinct()
                    .ToList()
            })
            .OrderByDescending(x => x.TotalCalls)
            .ToList();

        int totalCount = toolGroups.Count;

        // Calculate eligible messages count
        // Assistant messages are those where PlayerId is null
        IQueryable<Message> eligibleMessagesQuery = context.Messages
            .AsNoTracking()
            .Where(m => m.PlayerId == null);

        // Apply the same filters to eligible messages
        if (instructionVersionId.HasValue)
        {
            eligibleMessagesQuery = eligibleMessagesQuery.Where(m => m.InstructionVersionId == instructionVersionId.Value);
        }
        else if (!string.IsNullOrEmpty(agentId))
        {
            eligibleMessagesQuery = eligibleMessagesQuery.Where(m => m.InstructionVersion != null && m.InstructionVersion.AgentId == agentId);
        }

        if (gameId.HasValue)
        {
            eligibleMessagesQuery = eligibleMessagesQuery.Where(m => m.GameId == gameId.Value);
        }

        int eligibleMessagesCount = await eligibleMessagesQuery.CountAsync(cancellationToken);

        // Apply pagination
        List<ToolUsageItemDto> items = toolGroups
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(grp => new ToolUsageItemDto
            {
                ToolName = grp.ToolName,
                TotalCalls = grp.TotalCalls,
                EligibleMessages = eligibleMessagesCount,
                UsagePercentage = eligibleMessagesCount > 0
                    ? Math.Round((double)grp.TotalCalls / eligibleMessagesCount * 100, 2)
                    : 0,
                EnabledAgents = grp.EnabledAgents
            })
            .ToList();

        return new ToolUsageListResponse
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
