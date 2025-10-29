using MattEland.Jaimes.Services.Models;

namespace MattEland.Jaimes.ServiceLayer.Services;

public interface IScenariosService
{
 Task<ScenarioDto[]> GetScenariosAsync(CancellationToken cancellationToken = default);
}
