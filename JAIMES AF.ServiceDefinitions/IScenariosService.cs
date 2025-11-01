using MattEland.Jaimes.Domain;

namespace MattEland.Jaimes.ServiceDefinitions;

public interface IScenariosService
{
    Task<ScenarioDto[]> GetScenariosAsync(CancellationToken cancellationToken = default);
}
