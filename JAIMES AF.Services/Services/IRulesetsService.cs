using MattEland.Jaimes.Services.Models;

namespace MattEland.Jaimes.ServiceLayer.Services;

public interface IRulesetsService
{
 Task<RulesetDto[]> GetRulesetsAsync(CancellationToken cancellationToken = default);
}
