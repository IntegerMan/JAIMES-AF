using Riok.Mapperly.Abstractions;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.Services.Models;
using System.Collections.Generic;

namespace MattEland.Jaimes.ServiceLayer.Services;

[Mapper]
public static partial class RulesetMapper
{
 public static partial RulesetDto ToDto(this Ruleset ruleset);
 public static partial RulesetDto[] ToDto(this IEnumerable<Ruleset> rulesets);
}
