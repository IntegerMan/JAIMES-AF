using MattEland.Jaimes.Domain;
using MattEland.Jaimes.Repositories.Entities;
using Riok.Mapperly.Abstractions;

namespace MattEland.Jaimes.ServiceLayer.Mapping;

[Mapper]
public static partial class PlayerMapper
{
    public static partial PlayerDto ToDto(this Player player);
    public static partial PlayerDto[] ToDto(this IEnumerable<Player> players);
}
