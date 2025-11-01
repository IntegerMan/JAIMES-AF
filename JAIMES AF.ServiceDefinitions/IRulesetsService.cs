using MattEland.Jaimes.Domain;

namespace MattEland.Jaimes.ServiceDefinitions;

public interface IRulesetsService
{
    Task<RulesetDto[]> GetRulesetsAsync(CancellationToken cancellationToken = default);
}
