using MattEland.Jaimes.Domain;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.ServiceLayer.Mapping;
using Microsoft.EntityFrameworkCore;

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
                .ToListAsync(cancellationToken);

            allMessages = allMessages.Concat(messagesAfter).ToList();
        }

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

    public async Task<IEnumerable<MessageContextDto>> GetMessagesByAgentAsync(string agentId,
        int? versionId,
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Filter by AgentId (case-insensitive) AND IsScriptedMessage == false
        // Note: AgentId is stored as a string in the DB, so we need to ensure format matches
        string agentIdLower = agentId.ToLower();

        var query = context.Messages
            .AsNoTracking()
            .Where(m => m.AgentId.ToLower() == agentIdLower && !m.IsScriptedMessage);

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

        var dtos = new List<MessageContextDto>();
        foreach (var message in messages)
        {
            var dto = message.ToContextDto();

            // Populate Game Title in DTO if available (assuming DTO has property or we map it)
            // MessageContextDto doesn't explicitly have GameTitle, but MessageDto might not either.
            // We might need to rely on GameId or add GameTitle to MessageDto/ContextDto.
            // For now, let's proceed with standard mapping. The UI can fetch Game info or we use GameId.
            // Wait, plan said "Group by Game". Filtering by GameId is easy. Displaying Game Title needs the title.
            // MessageResponse has GameId?
            // I'll check if I need to add GameTitle to DTO.

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
