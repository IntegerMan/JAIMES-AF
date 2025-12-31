using MattEland.Jaimes.Domain;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.ServiceLayer.Mapping;
using Microsoft.EntityFrameworkCore;

namespace MattEland.Jaimes.ServiceLayer.Services;

public class MessageService(IDbContextFactory<JaimesDbContext> contextFactory) : IMessageService
{
    public async Task<IEnumerable<MessageContextDto>> GetMessageContextAsync(int messageId, int count,
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

        // Get the last N messages for this game up to and including the target message
        // We order by ID descending to get the most recent ones, then take count, then reverse to chronological order
        // Note: We filter by GameId to ensure we're only looking at the relevant conversation
        List<Message> messages = await context.Messages
            .AsNoTracking()
            .Where(m => m.GameId == targetMessage.GameId && m.Id <= messageId)
            .OrderByDescending(m => m.Id)
            .Take(count)
            .Include(m => m.Player)
            .Include(m => m.ChatHistory)
            .Include(m => m.ToolCalls)
            .Include(m => m.MessageSentiment)
            .Include(m => m.InstructionVersion)
            .ToListAsync(cancellationToken);

        // Fetch metrics and feedback manually since navigation properties aren't configured
        var messageIds = messages.Select(m => m.Id).ToList();

        var metrics = await context.MessageEvaluationMetrics
            .AsNoTracking()
            .Where(m => messageIds.Contains(m.MessageId))
            .ToListAsync(cancellationToken);

        var feedbacks = await context.MessageFeedbacks
            .AsNoTracking()
            .Where(f => messageIds.Contains(f.MessageId))
            .ToListAsync(cancellationToken);

        // Reverse to return them in chronological order
        messages.Reverse();

        var dtos = new List<MessageContextDto>();
        foreach (var message in messages)
        {
            var dto = message.ToContextDto();
            dto.Metrics = metrics
                .Where(m => m.MessageId == message.Id)
                .Select(MessageMapper.ToResponse)
                .ToList();

            var feedback = feedbacks.FirstOrDefault(f => f.MessageId == message.Id);
            if (feedback != null)
            {
                dto.Feedback = MessageMapper.ToResponse(feedback);
            }

            dtos.Add(dto);
        }

        return dtos;
    }
}
