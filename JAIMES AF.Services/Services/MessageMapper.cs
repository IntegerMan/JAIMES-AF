using Riok.Mapperly.Abstractions;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.Services.Models;
using System.Collections.Generic;

namespace MattEland.Jaimes.ServiceLayer.Services;

[Mapper]
public static partial class MessageMapper
{
 public static partial MessageDto ToDto(this Message message);
 public static partial MessageDto[] ToDto(this IEnumerable<Message> messages);
}
