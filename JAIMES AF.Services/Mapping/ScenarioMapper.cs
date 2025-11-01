using MattEland.Jaimes.Domain;
using MattEland.Jaimes.Repositories.Entities;
using Riok.Mapperly.Abstractions;

namespace MattEland.Jaimes.ServiceLayer.Mapping;

[Mapper]
public static partial class ScenarioMapper
{
    public static partial ScenarioDto ToDto(this Scenario scenario);
    public static partial ScenarioDto[] ToDto(this IEnumerable<Scenario> scenarios);
}
