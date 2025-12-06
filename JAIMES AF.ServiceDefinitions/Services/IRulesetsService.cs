namespace MattEland.Jaimes.ServiceDefinitions.Services;

public interface IRulesetsService
{
    Task<RulesetDto[]> GetRulesetsAsync(CancellationToken cancellationToken = default);
    Task<RulesetDto> GetRulesetAsync(string id, CancellationToken cancellationToken = default);
    Task<RulesetDto> CreateRulesetAsync(string id, string name, CancellationToken cancellationToken = default);
    Task<RulesetDto> UpdateRulesetAsync(string id, string name, CancellationToken cancellationToken = default);
}