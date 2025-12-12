using MattEland.Jaimes.ServiceDefinitions.Responses;
using Microsoft.Extensions.DependencyInjection;

namespace MattEland.Jaimes.Tools;

/// <summary>
/// Tool that provides rules search functionality using Kernel Memory RAG.
/// </summary>
public class RulesSearchTool(GameDto game, IServiceProvider serviceProvider)
{
    private readonly GameDto _game = game ?? throw new ArgumentNullException(nameof(game));

    private readonly IServiceProvider _serviceProvider =
        serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    /// <summary>
    /// Searches the ruleset's rules to find answers to specific questions or queries.
    /// This tool uses RAG (Retrieval-Augmented Generation) to find relevant rules from the indexed ruleset.
    /// Results are always stored for diagnostic purposes when called from agent tool calls.
    /// </summary>
    /// <param name="query">The question or query about the rules. For example: "How do I calculate damage?" or "What are the rules for skill checks?"</param>
    /// <returns>A string containing the answer or relevant rules information based on the query.</returns>
    [Description(
        "Searches the ruleset's indexed rules to find answers to specific questions or queries. This is a rules search tool that gets answers from rules to specific questions or queries. Use this tool whenever you need to look up game rules, mechanics, or rule clarifications. The tool will search through the indexed rules for the current scenario's ruleset and return relevant information.")]
    public async Task<string> SearchRulesAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return "Please provide a query or question about the rules.";

        string rulesetId = _game.Ruleset.Id;

        // Create a scope to resolve IRulesSearchService on each call
        // This ensures we get a fresh scoped instance and avoid ObjectDisposedException
        // when the tool outlives the scope that created it
        using IServiceScope scope = _serviceProvider.CreateScope();
        IRulesSearchService? rulesSearchService = scope.ServiceProvider.GetService<IRulesSearchService>();
        if (rulesSearchService == null)
        {
            return "Rules search service is not available.";
        }

        // Use SearchRulesDetailedAsync to get detailed results for storage
        // Results are always stored when called from agent tool calls
        SearchRulesResponse response = await rulesSearchService.SearchRulesDetailedAsync(rulesetId, query, true);

        if (response.Results.Length == 0) return "No relevant rules found for your query.";

        // Format results into a readable string for the agent
        List<string> resultTexts = response.Results.Select(r => r.Text).ToList();
        return string.Join("\n\n---\n\n", resultTexts);
    }
}