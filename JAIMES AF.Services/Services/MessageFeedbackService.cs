using MattEland.Jaimes.Domain;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.ServiceLayer.Mapping;
using Microsoft.EntityFrameworkCore;

namespace MattEland.Jaimes.ServiceLayer.Services;

public class MessageFeedbackService(IDbContextFactory<JaimesDbContext> contextFactory) : IMessageFeedbackService
{
    public async Task<MessageFeedbackDto> SubmitFeedbackAsync(int messageId,
        bool isPositive,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Validate that the message exists
        Message? message = await context.Messages
            .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);

        if (message == null)
            throw new ArgumentException($"Message with id '{messageId}' does not exist.", nameof(messageId));

        // Validate that this is an assistant message (PlayerId is null)
        if (message.PlayerId != null)
            throw new ArgumentException(
                $"Feedback can only be submitted for assistant messages. Message '{messageId}' is a player message.",
                nameof(messageId));

        // Check if feedback already exists for this message
        MessageFeedback? existingFeedback = await context.MessageFeedbacks
            .FirstOrDefaultAsync(mf => mf.MessageId == messageId, cancellationToken);

        if (existingFeedback != null)
            throw new ArgumentException(
                $"Feedback already exists for message '{messageId}'. Each message can only have one feedback entry.",
                nameof(messageId));

        // Create new feedback
        MessageFeedback feedback = new()
        {
            MessageId = messageId,
            IsPositive = isPositive,
            Comment = comment,
            CreatedAt = DateTime.UtcNow,
            InstructionVersionId = message.InstructionVersionId // Copy from message for tracking
        };

        context.MessageFeedbacks.Add(feedback);
        await context.SaveChangesAsync(cancellationToken);

        return feedback.ToDto();
    }

    public async Task<MessageFeedbackDto?> GetFeedbackForMessageAsync(int messageId,
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        MessageFeedback? feedback = await context.MessageFeedbacks
            .AsNoTracking()
            .FirstOrDefaultAsync(mf => mf.MessageId == messageId, cancellationToken);

        return feedback?.ToDto();
    }

    public async Task<MattEland.Jaimes.ServiceDefinitions.Responses.FeedbackListResponse> GetFeedbackListAsync(int page,
        int pageSize,
        AdminFilterParams? filters = null,
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        IQueryable<MessageFeedback> query = context.MessageFeedbacks
            .AsNoTracking()
            .Where(mf => !mf.Message!.IsScriptedMessage)
            .Include(mf => mf.Message)
            .ThenInclude(m => m!.Game)
            .ThenInclude(g => g!.Player)
            .Include(mf => mf.Message)
            .ThenInclude(m => m!.Game)
            .ThenInclude(g => g!.Scenario)
            .Include(mf => mf.Message)
            .Include(mf => mf.Message)
            .ThenInclude(m => m!.ToolCalls)
            .Include(mf => mf.Message)
            .ThenInclude(m => m!.InstructionVersion)
            .Include(mf => mf.InstructionVersion);

        // Apply filters from AdminFilterParams
        if (filters != null)
        {
            // Apply isPositive filter if specified
            if (filters.IsPositive.HasValue)
            {
                query = query.Where(mf => mf.IsPositive == filters.IsPositive.Value);
            }

            // Apply toolName filter if specified - filter by messages that have a tool call with this name
            if (!string.IsNullOrEmpty(filters.ToolName))
            {
                query = query.Where(mf => mf.Message != null &&
                                          mf.Message.ToolCalls.Any(tc =>
                                              tc.ToolName.ToLower() == filters.ToolName.ToLower()));
            }

            // Apply AgentId filter if specified
            if (!string.IsNullOrEmpty(filters.AgentId))
            {
                query = query.Where(mf =>
                    (mf.InstructionVersion != null && mf.InstructionVersion.AgentId == filters.AgentId) ||
                    (mf.Message != null && mf.Message.InstructionVersion != null &&
                     mf.Message.InstructionVersion.AgentId == filters.AgentId) ||
                    (mf.Message != null && mf.Message.AgentId == filters.AgentId));
            }

            // Apply InstructionVersionId filter if specified
            if (filters.InstructionVersionId.HasValue)
            {
                query = query.Where(mf =>
                    mf.InstructionVersionId == filters.InstructionVersionId.Value ||
                    (mf.Message != null && mf.Message.InstructionVersionId == filters.InstructionVersionId.Value));
            }

            // Apply GameId filter if specified
            if (filters.GameId.HasValue)
            {
                query = query.Where(mf => mf.Message != null && mf.Message.GameId == filters.GameId.Value);
            }
        }

        query = query.OrderByDescending(mf => mf.CreatedAt);

        int totalCount = await query.CountAsync(cancellationToken);

        List<MessageFeedback> items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        IEnumerable<MattEland.Jaimes.ServiceDefinitions.Responses.FeedbackListItemDto> dtos = items.Select(mf =>
            new MattEland.Jaimes.ServiceDefinitions.Responses.FeedbackListItemDto
            {
                Id = mf.Id,
                MessageId = mf.MessageId,
                IsPositive = mf.IsPositive,
                Comment = mf.Comment,
                CreatedAt = mf.CreatedAt,
                InstructionVersionId = mf.InstructionVersionId ?? mf.Message?.InstructionVersionId,
                AgentId = mf.InstructionVersion?.AgentId ??
                          mf.Message?.InstructionVersion?.AgentId ?? mf.Message?.AgentId,
                AgentVersion = mf.InstructionVersion?.VersionNumber ?? mf.Message?.InstructionVersion?.VersionNumber,
                GameId = mf.Message?.GameId ?? Guid.Empty,
                GamePlayerName = mf.Message?.Game?.Player?.Name,
                GameScenarioName = mf.Message?.Game?.Scenario?.Name,
                GameRulesetId = mf.Message?.Game?.RulesetId,
                ToolNames = mf.Message?.ToolCalls?.Select(tc => tc.ToolName).Distinct().ToList()
            });

        return new MattEland.Jaimes.ServiceDefinitions.Responses.FeedbackListResponse
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<MattEland.Jaimes.ServiceDefinitions.Responses.FeedbackFullDetailsResponse?>
        GetFeedbackDetailsAsync(int id, CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        MessageFeedback? mf = await context.MessageFeedbacks
            .AsNoTracking()
            .Include(mf => mf.Message)
            .ThenInclude(m => m!.Game)
            .ThenInclude(g => g!.Player)
            .Include(mf => mf.Message)
            .ThenInclude(m => m!.Game)
            .ThenInclude(g => g!.Scenario)
            .Include(mf => mf.Message)
            .ThenInclude(m => m!.ToolCalls)
            .Include(mf => mf.Message)
            .ThenInclude(m => m!.InstructionVersion)
            .Include(mf => mf.InstructionVersion)
            .FirstOrDefaultAsync(mf => mf.Id == id, cancellationToken);

        if (mf == null) return null;

        return new MattEland.Jaimes.ServiceDefinitions.Responses.FeedbackFullDetailsResponse
        {
            Id = mf.Id,
            MessageId = mf.MessageId,
            IsPositive = mf.IsPositive,
            Comment = mf.Comment,
            CreatedAt = mf.CreatedAt,
            InstructionVersionId = mf.InstructionVersionId ?? mf.Message?.InstructionVersionId,
            AgentId = mf.InstructionVersion?.AgentId ?? mf.Message?.InstructionVersion?.AgentId ?? mf.Message?.AgentId,
            AgentVersion = mf.InstructionVersion?.VersionNumber ?? mf.Message?.InstructionVersion?.VersionNumber,
            GameId = mf.Message?.GameId ?? Guid.Empty,
            GamePlayerName = mf.Message?.Game?.Player?.Name,
            GameScenarioName = mf.Message?.Game?.Scenario?.Name,
            GameRulesetId = mf.Message?.Game?.RulesetId,
            ToolNames = mf.Message?.ToolCalls?.Select(tc => tc.ToolName).Distinct().ToList()
        };
    }

    public async Task<MattEland.Jaimes.ServiceDefinitions.Responses.FeedbackSummaryResponse> GetFeedbackSummaryAsync(
        AdminFilterParams? filters = null,
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        IQueryable<MessageFeedback> query = context.MessageFeedbacks
            .AsNoTracking()
            .Where(mf => !mf.Message!.IsScriptedMessage)
            .Include(mf => mf.Message)
            .ThenInclude(m => m!.ToolCalls)
            .Include(mf => mf.Message)
            .ThenInclude(m => m!.InstructionVersion)
            .Include(mf => mf.InstructionVersion);

        // Apply filters from AdminFilterParams
        if (filters != null)
        {
            // Apply isPositive filter if specified
            if (filters.IsPositive.HasValue)
            {
                query = query.Where(mf => mf.IsPositive == filters.IsPositive.Value);
            }

            // Apply toolName filter if specified - filter by messages that have a tool call with this name
            if (!string.IsNullOrEmpty(filters.ToolName))
            {
                query = query.Where(mf => mf.Message != null &&
                                          mf.Message.ToolCalls.Any(tc =>
                                              tc.ToolName.ToLower() == filters.ToolName.ToLower()));
            }

            // Apply AgentId filter if specified
            if (!string.IsNullOrEmpty(filters.AgentId))
            {
                query = query.Where(mf =>
                    (mf.InstructionVersion != null && mf.InstructionVersion.AgentId == filters.AgentId) ||
                    (mf.Message != null && mf.Message.InstructionVersion != null &&
                     mf.Message.InstructionVersion.AgentId == filters.AgentId) ||
                    (mf.Message != null && mf.Message.AgentId == filters.AgentId));
            }

            // Apply InstructionVersionId filter if specified
            if (filters.InstructionVersionId.HasValue)
            {
                query = query.Where(mf =>
                    mf.InstructionVersionId == filters.InstructionVersionId.Value ||
                    (mf.Message != null && mf.Message.InstructionVersionId == filters.InstructionVersionId.Value));
            }

            // Apply GameId filter if specified
            if (filters.GameId.HasValue)
            {
                query = query.Where(mf => mf.Message != null && mf.Message.GameId == filters.GameId.Value);
            }
        }

        int totalCount = await query.CountAsync(cancellationToken);
        int positiveCount = await query.CountAsync(mf => mf.IsPositive, cancellationToken);
        int negativeCount = await query.CountAsync(mf => !mf.IsPositive, cancellationToken);
        int withCommentsCount = await query.CountAsync(mf => !string.IsNullOrEmpty(mf.Comment), cancellationToken);

        return new MattEland.Jaimes.ServiceDefinitions.Responses.FeedbackSummaryResponse
        {
            TotalCount = totalCount,
            PositiveCount = positiveCount,
            NegativeCount = negativeCount,
            WithCommentsCount = withCommentsCount
        };
    }
}

