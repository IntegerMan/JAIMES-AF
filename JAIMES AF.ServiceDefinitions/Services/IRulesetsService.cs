namespace MattEland.Jaimes.ServiceDefinitions.Services;

public interface IRulesetsService
{
    Task<RulesetDto[]> GetRulesetsAsync(CancellationToken cancellationToken = default);
    Task<RulesetDto> GetRulesetAsync(string id, CancellationToken cancellationToken = default);
    Task<RulesetDto> CreateRulesetAsync(string id, string name, CancellationToken cancellationToken = default);

    Task<RulesetDto> UpdateRulesetAsync(string id, string name, string? description,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to create a ruleset if it doesn't already exist.
    /// Returns true if created, false if it already exists.
    /// </summary>
    Task<bool> TryCreateRulesetAsync(string id, string name, string? description,
        CancellationToken cancellationToken = default);
}