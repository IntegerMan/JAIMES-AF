using MattEland.Jaimes.Agents.ContextProviders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MattEland.Jaimes.Agents.Services;

/// <summary>
/// Factory for creating RulesTextSearchProvider instances for specific games.
/// </summary>
public class RulesTextSearchProviderFactory(IServiceProvider serviceProvider)
{
    private readonly IServiceProvider _serviceProvider =
        serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    /// <summary>
    /// Creates a RulesTextSearchProvider for the specified ruleset.
    /// </summary>
    /// <param name="rulesetId">The ID of the ruleset to search.</param>
    /// <param name="rootServiceProvider">The root service provider (for creating scopes that outlive the current request).</param>
    /// <returns>A configured RulesTextSearchProvider.</returns>
    public RulesTextSearchProvider CreateForRuleset(string rulesetId, IServiceProvider rootServiceProvider)
    {
        ILogger<RulesTextSearchProvider> logger =
            _serviceProvider.GetRequiredService<ILogger<RulesTextSearchProvider>>();
        IConfiguration configuration = _serviceProvider.GetRequiredService<IConfiguration>();
        return new RulesTextSearchProvider(rulesetId, rootServiceProvider, logger, configuration);
    }
}
