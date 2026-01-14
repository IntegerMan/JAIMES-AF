using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.EntityFrameworkCore;

namespace MattEland.Jaimes.ServiceLayer.Services;

/// <summary>
/// Service for sentiment list and details operations.
/// </summary>
public class MessageSentimentService(IDbContextFactory<JaimesDbContext> contextFactory) : IMessageSentimentService
{
    /// <inheritdoc />
    /// <inheritdoc />
    public async Task<SentimentListResponse> GetSentimentListAsync(
        int page,
        int pageSize,
        AdminFilterParams? filters = null,
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        IQueryable<MessageSentiment> query = context.MessageSentiments
            .AsNoTracking()
            .Include(s => s.Message)
            .ThenInclude(m => m!.Game)
            .ThenInclude(g => g!.Player)
            .Include(s => s.Message)
            .ThenInclude(m => m!.Game)
            .ThenInclude(g => g!.Scenario)
            .Include(s => s.Message)
            .ThenInclude(m => m!.ToolCalls)
            .Include(s => s.Message)
            .ThenInclude(m => m!.InstructionVersion)
            .Include(s => s.Message)
            .ThenInclude(m => m!.NextMessage)
            .Include(s => s.Message)
            .ThenInclude(m => m!.PreviousMessage)
            .ThenInclude(pm => pm!.ToolCalls);

        // Apply filters
        if (filters != null)
        {
            if (filters.GameId.HasValue)
            {
                query = query.Where(s => s.Message != null && s.Message.GameId == filters.GameId.Value);
            }

            if (!string.IsNullOrEmpty(filters.ToolName))
            {
                // Filter by tool calls on the previous assistant message (the one the user is reacting to)
                query = query.Where(s => s.Message != null &&
                                         s.Message.PreviousMessage != null &&
                                         s.Message.PreviousMessage.ToolCalls.Any(tc =>
                                             tc.ToolName.ToLower() == filters.ToolName.ToLower()));
            }

            if (filters.Sentiment.HasValue)
            {
                query = query.Where(s => s.Sentiment == filters.Sentiment.Value);
            }

            if (!string.IsNullOrEmpty(filters.AgentId))
            {
                query = query.Where(s => s.Message != null &&
                                         ((s.Message.InstructionVersion != null &&
                                           s.Message.InstructionVersion.AgentId == filters.AgentId) ||
                                          s.Message.AgentId == filters.AgentId));
            }

            if (filters.InstructionVersionId.HasValue)
            {
                // Sentiment is on user messages, so look at the previous message (AI response) for version filtering
                query = query.Where(s => s.Message != null &&
                                         s.Message.PreviousMessage != null &&
                                         s.Message.PreviousMessage.InstructionVersionId == filters.InstructionVersionId.Value);
            }

            // Apply feedback filters in the database query (Use NextMessageId to link to the response message)
            if (filters.HasFeedback.HasValue)
            {
                bool hasFeedback = filters.HasFeedback.Value;
                if (hasFeedback)
                {
                    query = query.Where(s => s.Message != null &&
                                             s.Message.NextMessageId.HasValue &&
                                             context.MessageFeedbacks.Any(f =>
                                                 f.MessageId == s.Message.NextMessageId.Value));
                }
                else
                {
                    query = query.Where(s => s.Message != null &&
                                             (!s.Message.NextMessageId.HasValue ||
                                              !context.MessageFeedbacks.Any(f =>
                                                  f.MessageId == s.Message.NextMessageId.Value)));
                }
            }

            if (filters.FeedbackType.HasValue)
            {
                int feedbackType = filters.FeedbackType.Value;
                if (feedbackType > 0) // Positive
                {
                    query = query.Where(s => s.Message != null &&
                                             s.Message.NextMessageId.HasValue &&
                                             context.MessageFeedbacks.Any(f =>
                                                 f.MessageId == s.Message.NextMessageId.Value && f.IsPositive));
                }
                else if (feedbackType < 0) // Negative
                {
                    query = query.Where(s => s.Message != null &&
                                             s.Message.NextMessageId.HasValue &&
                                             context.MessageFeedbacks.Any(f =>
                                                 f.MessageId == s.Message.NextMessageId.Value && !f.IsPositive));
                }
                else // Neutral (0) - No neutral feedback exists, so return nothing
                {
                    query = query.Where(s => false);
                }
            }
        }

        // Order by created date descending (newest first)
        query = query.OrderByDescending(s => s.CreatedAt);

        int totalCount = await query.CountAsync(cancellationToken);

        List<MessageSentiment> items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // Get feedback info for the AI response messages (next message after user message)
        Dictionary<int, MessageFeedback?> feedbackByUserMessageId = new();
        if (items.Count != 0)
        {
            // Collect next message IDs from the loaded items
            List<int> responseMessageIds = items
                .Where(s => s.Message?.NextMessageId.HasValue == true)
                .Select(s => s.Message!.NextMessageId!.Value)
                .Distinct()
                .ToList();

            // Get feedback for those response messages
            Dictionary<int, MessageFeedback> feedbackByResponseId = new();
            if (responseMessageIds.Count != 0)
            {
                List<MessageFeedback> feedbacks = await context.MessageFeedbacks
                    .AsNoTracking()
                    .Where(f => responseMessageIds.Contains(f.MessageId))
                    .ToListAsync(cancellationToken);

                feedbackByResponseId = feedbacks.ToDictionary(f => f.MessageId);
            }

            foreach (var item in items)
            {
                if (item.Message?.NextMessageId.HasValue == true &&
                    feedbackByResponseId.TryGetValue(item.Message.NextMessageId.Value, out var fb))
                {
                    feedbackByUserMessageId[item.MessageId] = fb;
                }
                else
                {
                    feedbackByUserMessageId[item.MessageId] = null;
                }
            }
        }

        IEnumerable<SentimentListItemDto> dtos = items.Select(s =>
        {
            feedbackByUserMessageId.TryGetValue(s.MessageId, out var feedback);

            return new SentimentListItemDto
            {
                Id = s.Id,
                MessageId = s.MessageId,
                Sentiment = s.Sentiment,
                SentimentSource = (int) s.SentimentSource,
                Confidence = s.Confidence,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt,
                GameId = s.Message?.GameId ?? Guid.Empty,
                GamePlayerName = s.Message?.Game?.Player?.Name,
                GameScenarioName = s.Message?.Game?.Scenario?.Name,
                GameRulesetId = s.Message?.Game?.RulesetId,
                AgentVersion = s.Message?.InstructionVersion?.VersionNumber,
                AgentId = s.Message?.InstructionVersion?.AgentId ?? s.Message?.AgentId,
                InstructionVersionId = s.Message?.InstructionVersionId,
                // Tool names from the previous assistant message (the one the user is reacting to)
                ToolNames = s.Message?.PreviousMessage?.ToolCalls?.Select(tc => tc.ToolName).Distinct().ToList(),
                HasFeedback = feedback != null,
                FeedbackIsPositive = feedback?.IsPositive,
                MessagePreview = s.Message?.Text is string text && text.Length > 50
                    ? text[..50] + "..."
                    : s.Message?.Text
            };
        });

        return new SentimentListResponse
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<SentimentFullDetailsResponse?> GetSentimentDetailsAsync(
        int messageId,
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Try to locate the sentiment by message ID first (primary path), then fall back to sentiment ID for backward compatibility.
        MessageSentiment? sentiment = await context.MessageSentiments
            .AsNoTracking()
            .Include(s => s.Message)
            .ThenInclude(m => m!.Game)
            .ThenInclude(g => g!.Player)
            .Include(s => s.Message)
            .ThenInclude(m => m!.Game)
            .ThenInclude(g => g!.Scenario)
            .Include(s => s.Message)
            .ThenInclude(m => m!.ToolCalls)
            .Include(s => s.Message)
            .ThenInclude(m => m!.InstructionVersion)
            .Include(s => s.Message)
            .ThenInclude(m => m!.PreviousMessage)
            .ThenInclude(pm => pm!.ToolCalls)
            .FirstOrDefaultAsync(s => s.MessageId == messageId, cancellationToken);

        if (sentiment == null)
        {
            sentiment = await context.MessageSentiments
                .AsNoTracking()
                .Include(s => s.Message)
                .ThenInclude(m => m!.Game)
                .ThenInclude(g => g!.Player)
                .Include(s => s.Message)
                .ThenInclude(m => m!.Game)
                .ThenInclude(g => g!.Scenario)
                .Include(s => s.Message)
                .ThenInclude(m => m!.ToolCalls)
                .Include(s => s.Message)
                .ThenInclude(m => m!.InstructionVersion)
                .Include(s => s.Message)
                .ThenInclude(m => m!.PreviousMessage)
                .ThenInclude(pm => pm!.ToolCalls)
                .FirstOrDefaultAsync(s => s.Id == messageId, cancellationToken);
        }

        if (sentiment == null) return null;

        // Get feedback for the AI response (next message after this user message)
        MessageFeedback? feedback = null;
        if (sentiment.Message != null)
        {
            // Ensure we have the NextMessageId loaded
            if (sentiment.Message.NextMessageId == null)
            {
                // Try to load it if it wasn't eagerly loaded (though we added Include above, checking just in case)
                var msg = await context.Messages
                    .AsNoTracking()
                    .Where(m => m.Id == sentiment.MessageId)
                    .Select(m => new {m.NextMessageId})
                    .FirstOrDefaultAsync(cancellationToken);

                if (msg?.NextMessageId != null)
                {
                    sentiment.Message.NextMessageId = msg.NextMessageId;
                }
            }

            if (sentiment.Message.NextMessageId.HasValue)
            {
                feedback = await context.MessageFeedbacks
                    .AsNoTracking()
                    .FirstOrDefaultAsync(f => f.MessageId == sentiment.Message.NextMessageId.Value, cancellationToken);
            }
        }

        return new SentimentFullDetailsResponse
        {
            Id = sentiment.Id,
            MessageId = sentiment.MessageId,
            Sentiment = sentiment.Sentiment,
            SentimentSource = (int) sentiment.SentimentSource,
            Confidence = sentiment.Confidence,
            CreatedAt = sentiment.CreatedAt,
            UpdatedAt = sentiment.UpdatedAt,
            GameId = sentiment.Message?.GameId ?? Guid.Empty,
            GamePlayerName = sentiment.Message?.Game?.Player?.Name,
            GameScenarioName = sentiment.Message?.Game?.Scenario?.Name,
            GameRulesetId = sentiment.Message?.Game?.RulesetId,
            AgentVersion = sentiment.Message?.InstructionVersion?.VersionNumber,
            AgentId = sentiment.Message?.InstructionVersion?.AgentId ?? sentiment.Message?.AgentId,
            // Tool names from the previous assistant message (the one the user is reacting to)
            ToolNames = sentiment.Message?.PreviousMessage?.ToolCalls?.Select(tc => tc.ToolName).Distinct().ToList(),
            HasFeedback = feedback != null,
            FeedbackIsPositive = feedback?.IsPositive,
            FeedbackComment = feedback?.Comment,
            MessageText = sentiment.Message?.Text
        };
    }

    /// <inheritdoc />
    public async Task<SentimentSummaryResponse> GetSentimentSummaryAsync(AdminFilterParams? filters = null, CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        IQueryable<MessageSentiment> query = context.MessageSentiments.AsNoTracking();

        if (filters != null)
        {
            if (filters.GameId.HasValue)
            {
                query = query.Where(s => s.Message != null && s.Message.GameId == filters.GameId.Value);
            }

            if (!string.IsNullOrEmpty(filters.ToolName))
            {
                query = query.Where(s => s.Message != null &&
                                         s.Message.PreviousMessage != null &&
                                         s.Message.PreviousMessage.ToolCalls.Any(tc => tc.ToolName.ToLower() == filters.ToolName.ToLower()));
            }

            if (filters.Sentiment.HasValue)
            {
                query = query.Where(s => s.Sentiment == filters.Sentiment.Value);
            }

            if (!string.IsNullOrEmpty(filters.AgentId))
            {
                query = query.Where(s => s.Message != null &&
                                         ((s.Message.InstructionVersion != null &&
                                           s.Message.InstructionVersion.AgentId == filters.AgentId) ||
                                          s.Message.AgentId == filters.AgentId));
            }

            if (filters.InstructionVersionId.HasValue)
            {
                // Sentiment is on user messages, so look at the previous message (AI response) for version filtering
                query = query.Where(s => s.Message != null &&
                                         s.Message.PreviousMessage != null &&
                                         s.Message.PreviousMessage.InstructionVersionId == filters.InstructionVersionId.Value);
            }

            if (filters.HasFeedback.HasValue)
            {
                bool hasFeedback = filters.HasFeedback.Value;
                if (hasFeedback)
                {
                    query = query.Where(s => s.Message != null &&
                                             s.Message.NextMessageId.HasValue &&
                                             context.MessageFeedbacks.Any(f => f.MessageId == s.Message.NextMessageId.Value));
                }
                else
                {
                    query = query.Where(s => s.Message != null &&
                                             (!s.Message.NextMessageId.HasValue ||
                                              !context.MessageFeedbacks.Any(f => f.MessageId == s.Message.NextMessageId.Value)));
                }
            }

            if (filters.FeedbackType.HasValue)
            {
                int feedbackType = filters.FeedbackType.Value;
                if (feedbackType > 0)
                {
                    query = query.Where(s => s.Message != null &&
                                             s.Message.NextMessageId.HasValue &&
                                             context.MessageFeedbacks.Any(f => f.MessageId == s.Message.NextMessageId.Value && f.IsPositive));
                }
                else if (feedbackType < 0)
                {
                    query = query.Where(s => s.Message != null &&
                                             s.Message.NextMessageId.HasValue &&
                                             context.MessageFeedbacks.Any(f => f.MessageId == s.Message.NextMessageId.Value && !f.IsPositive));
                }
                else
                {
                    query = query.Where(s => false);
                }
            }
        }

        int totalCount = await query.CountAsync(cancellationToken);
        int positiveCount = await query.Where(s => s.Sentiment == 1).CountAsync(cancellationToken);
        int neutralCount = await query.Where(s => s.Sentiment == 0).CountAsync(cancellationToken);
        int negativeCount = await query.Where(s => s.Sentiment == -1).CountAsync(cancellationToken);

        double? avgConfidence = null;
        if (await query.AnyAsync(s => s.Confidence.HasValue, cancellationToken))
        {
            avgConfidence = await query.Where(s => s.Confidence.HasValue).AverageAsync(s => s.Confidence!.Value, cancellationToken);
        }

        return new SentimentSummaryResponse
        {
            TotalCount = totalCount,
            PositiveCount = positiveCount,
            NeutralCount = neutralCount,
            NegativeCount = negativeCount,
            AverageConfidence = avgConfidence
        };
    }
}
