namespace MattEland.Jaimes.ServiceDefinitions.Services;

public interface IRulesSearchService
{
    Task<string> SearchRulesAsync(string rulesetId, string query, CancellationToken cancellationToken = default);
    Task IndexRuleAsync(string rulesetId, string ruleId, string title, string content, CancellationToken cancellationToken = default);
    Task EnsureRulesetIndexedAsync(string rulesetId, CancellationToken cancellationToken = default);
}

