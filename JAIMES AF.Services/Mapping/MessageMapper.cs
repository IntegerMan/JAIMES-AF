using MattEland.Jaimes.Domain;
using MattEland.Jaimes.Repositories.Entities;
using Riok.Mapperly.Abstractions;

namespace MattEland.Jaimes.ServiceLayer.Mapping;

[Mapper]
public static partial class MessageMapper
{
    public static partial MessageDto ToDto(this Message message);
    public static partial MessageDto[] ToDto(this IEnumerable<Message> messages);
}
