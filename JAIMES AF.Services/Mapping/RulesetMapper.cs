namespace MattEland.Jaimes.ServiceLayer.Mapping;

[Mapper]
public static partial class RulesetMapper
{
    [MapperIgnoreSource(nameof(Ruleset.Players))]
    [MapperIgnoreSource(nameof(Ruleset.Scenarios))]
    [MapperIgnoreSource(nameof(Ruleset.Games))]
    public static partial RulesetDto ToDto(this Ruleset ruleset);

    public static partial RulesetDto[] ToDto(this IEnumerable<Ruleset> rulesets);
}