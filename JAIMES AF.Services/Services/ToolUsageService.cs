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
    IToolRegistry toolRegistry) : IToolUsageService
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

        // Group by tool name to get statistics for tools that have been called
        Dictionary<string, (int TotalCalls, List<string> EnabledAgents)> toolCallStats = allToolCalls
            .GroupBy(mtc => mtc.ToolName)
            .ToDictionary(
                grp => grp.Key,
                grp => (
                    TotalCalls: grp.Count(),
                    EnabledAgents: grp
                        .Where(mtc => mtc.InstructionVersion?.Agent != null)
                        .Select(mtc => $"{mtc.InstructionVersion!.Agent!.Name} v{mtc.InstructionVersion.VersionNumber}")
                        .Distinct()
                        .ToList()
                ));

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

                return new ToolUsageItemDto
                {
                    ToolName = tool.Name,
                    TotalCalls = totalCalls,
                    EligibleMessages = eligibleMessagesCount,
                    UsagePercentage = eligibleMessagesCount > 0
                        ? Math.Round((double)totalCalls / eligibleMessagesCount * 100, 2)
                        : 0,
                    EnabledAgents = enabledAgents
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
                        ? Math.Round((double)stats.TotalCalls / eligibleMessagesCount * 100, 2)
                        : 0,
                    EnabledAgents = stats.EnabledAgents
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
}
