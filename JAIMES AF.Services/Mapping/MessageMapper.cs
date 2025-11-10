using System.Linq;
using System.Collections.Generic;
using MattEland.Jaimes.Domain;
using MattEland.Jaimes.Repositories.Entities;

namespace MattEland.Jaimes.ServiceLayer.Mapping;

public static partial class MessageMapper
{
    public static MessageDto ToDto(this Message message)
    {
        return new MessageDto(
            message.Text,
            message.PlayerId,
            message.Player?.Name ?? "Game Master",
            message.CreatedAt);
    }

    public static MessageDto[] ToDto(this IEnumerable<Message>? messages)
    {
        return messages?.Select(m => m.ToDto()).ToArray() ?? [];
    }
}
