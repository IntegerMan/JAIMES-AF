using MattEland.Jaimes.Domain;

namespace MattEland.Jaimes.ServiceDefinitions.Services;

public interface IRulesetsService
{
    Task<RulesetDto[]> GetRulesetsAsync(CancellationToken cancellationToken = default);
}
