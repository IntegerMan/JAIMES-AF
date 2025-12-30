using MattEland.Jaimes.Domain;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.ServiceLayer.Mapping;
using Microsoft.EntityFrameworkCore;

namespace MattEland.Jaimes.ServiceLayer.Services;

public class MessageFeedbackService(IDbContextFactory<JaimesDbContext> contextFactory) : IMessageFeedbackService
{
    public async Task<MessageFeedbackDto> SubmitFeedbackAsync(int messageId, bool isPositive, string? comment,
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
        int pageSize, string? toolName = null, bool? isPositive = null, CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        IQueryable<MessageFeedback> query = context.MessageFeedbacks
            .AsNoTracking()
            .Include(mf => mf.Message)
            .ThenInclude(m => m!.Game)
            .ThenInclude(g => g!.Player)
            .Include(mf => mf.Message)
            .ThenInclude(m => m!.Game)
            .ThenInclude(g => g!.Scenario)
            .Include(mf => mf.Message)
            .ThenInclude(m => m!.ToolCalls)
            .Include(mf => mf.InstructionVersion);

        // Apply isPositive filter if specified
        if (isPositive.HasValue)
        {
            query = query.Where(mf => mf.IsPositive == isPositive.Value);
        }

        // Apply toolName filter if specified - filter by messages that have a tool call with this name
        if (!string.IsNullOrEmpty(toolName))
        {
            query = query.Where(mf => mf.Message != null &&
                                      mf.Message.ToolCalls.Any(tc => tc.ToolName.ToLower() == toolName.ToLower()));
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
                InstructionVersionId = mf.InstructionVersionId,
                AgentVersion = mf.InstructionVersion?.VersionNumber,
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
}

