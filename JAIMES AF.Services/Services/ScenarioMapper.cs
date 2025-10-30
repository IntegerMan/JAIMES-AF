using Riok.Mapperly.Abstractions;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.Services.Models;
using System.Collections.Generic;

namespace MattEland.Jaimes.ServiceLayer.Services;

[Mapper]
public static partial class ScenarioMapper
{
 public static partial ScenarioDto ToDto(this Scenario scenario);
 public static partial ScenarioDto[] ToDto(this IEnumerable<Scenario> scenarios);
}
