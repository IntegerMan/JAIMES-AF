using MattEland.Jaimes.Domain;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.ServiceLayer.Mapping;
using Microsoft.EntityFrameworkCore;

namespace MattEland.Jaimes.ServiceLayer.Services;

public class MessageToolCallService(IDbContextFactory<JaimesDbContext> contextFactory) : IMessageToolCallService
{
    public async Task<IReadOnlyList<MessageToolCallDto>> GetToolCallsForMessageAsync(int messageId,
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Validate that the message exists
        bool messageExists = await context.Messages.AnyAsync(m => m.Id == messageId, cancellationToken);
        if (!messageExists)
        {
            throw new ArgumentException($"Message with ID {messageId} not found.", nameof(messageId));
        }

        List<MessageToolCall> toolCalls = await context.MessageToolCalls
            .AsNoTracking()
            .Where(mtc => mtc.MessageId == messageId)
            .OrderBy(mtc => mtc.CreatedAt)
            .ToListAsync(cancellationToken);

        return toolCalls.ToDto();
    }
}



