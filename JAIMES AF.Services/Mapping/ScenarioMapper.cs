namespace MattEland.Jaimes.ServiceLayer.Mapping;

[Mapper]
public static partial class ScenarioMapper
{
    [MapperIgnoreSource(nameof(Scenario.Ruleset))]
    [MapperIgnoreSource(nameof(Scenario.Games))]
    [MapperIgnoreSource(nameof(Scenario.ScenarioAgents))]
    public static partial ScenarioDto ToDto(this Scenario scenario);

    public static partial ScenarioDto[] ToDto(this IEnumerable<Scenario> scenarios);
}