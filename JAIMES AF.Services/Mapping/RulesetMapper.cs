using MattEland.Jaimes.Domain;
using MattEland.Jaimes.Repositories.Entities;
using Riok.Mapperly.Abstractions;

namespace MattEland.Jaimes.ServiceLayer.Mapping;

[Mapper]
public static partial class RulesetMapper
{
    public static partial RulesetDto ToDto(this Ruleset ruleset);
    public static partial RulesetDto[] ToDto(this IEnumerable<Ruleset> rulesets);
}
