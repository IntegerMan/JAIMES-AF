using MattEland.Jaimes.Domain;

namespace MattEland.Jaimes.ServiceDefinitions.Services;

public interface IScenariosService
{
    Task<ScenarioDto[]> GetScenariosAsync(CancellationToken cancellationToken = default);
}
