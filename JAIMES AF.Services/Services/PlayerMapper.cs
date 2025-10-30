using Riok.Mapperly.Abstractions;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.Services.Models;
using System.Collections.Generic;

namespace MattEland.Jaimes.ServiceLayer.Services;

[Mapper]
public static partial class PlayerMapper
{
    public static partial PlayerDto ToDto(this Player player);
    public static partial PlayerDto[] ToDto(this IEnumerable<Player> players);
}
