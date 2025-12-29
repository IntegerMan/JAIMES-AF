using MattEland.Jaimes.Domain;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.ServiceLayer.Mapping;
using Microsoft.EntityFrameworkCore;

namespace MattEland.Jaimes.ServiceLayer.Services;

public class MessageFeedbackService(IDbContextFactory<JaimesDbContext> contextFactory) : IMessageFeedbackService
{
    public async Task<MessageFeedbackDto> SubmitFeedbackAsync(int messageId, bool isPositive, string? comment, CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Validate that the message exists
        Message? message = await context.Messages
            .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);

        if (message == null)
            throw new ArgumentException($"Message with id '{messageId}' does not exist.", nameof(messageId));

        // Validate that this is an assistant message (PlayerId is null)
        if (message.PlayerId != null)
            throw new ArgumentException($"Feedback can only be submitted for assistant messages. Message '{messageId}' is a player message.", nameof(messageId));

        // Check if feedback already exists for this message
        MessageFeedback? existingFeedback = await context.MessageFeedbacks
            .FirstOrDefaultAsync(mf => mf.MessageId == messageId, cancellationToken);

        if (existingFeedback != null)
            throw new ArgumentException($"Feedback already exists for message '{messageId}'. Each message can only have one feedback entry.", nameof(messageId));

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

    public async Task<MessageFeedbackDto?> GetFeedbackForMessageAsync(int messageId, CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        MessageFeedback? feedback = await context.MessageFeedbacks
            .AsNoTracking()
            .FirstOrDefaultAsync(mf => mf.MessageId == messageId, cancellationToken);

        return feedback?.ToDto();
    }
}

