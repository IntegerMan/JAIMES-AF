namespace MattEland.Jaimes.ServiceLayer.Mapping;

[Mapper]
public static partial class PlayerMapper
{
    [MapperIgnoreSource(nameof(Player.Ruleset))]
    [MapperIgnoreSource(nameof(Player.Games))]
    public static partial PlayerDto ToDto(this Player player);

    public static partial PlayerDto[] ToDto(this IEnumerable<Player> players);
}