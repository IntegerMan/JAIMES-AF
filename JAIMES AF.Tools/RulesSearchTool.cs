using System.ComponentModel;
using MattEland.Jaimes.Domain;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.Tools;

/// <summary>
/// Tool that provides rules search functionality using Kernel Memory RAG.
/// </summary>
public class RulesSearchTool
{
    private readonly GameDto _game;
    private readonly IRulesSearchService _rulesSearchService;

    public RulesSearchTool(GameDto game, IRulesSearchService rulesSearchService)
    {
        _game = game ?? throw new ArgumentNullException(nameof(game));
        _rulesSearchService = rulesSearchService ?? throw new ArgumentNullException(nameof(rulesSearchService));
    }

    /// <summary>
    /// Searches the ruleset's rules to find answers to specific questions or queries.
    /// This tool uses RAG (Retrieval-Augmented Generation) to find relevant rules from the indexed ruleset.
    /// </summary>
    /// <param name="query">The question or query about the rules. For example: "How do I calculate damage?" or "What are the rules for skill checks?"</param>
    /// <returns>A string containing the answer or relevant rules information based on the query.</returns>
    [Description("Searches the ruleset's indexed rules to find answers to specific questions or queries. This is a rules search tool that gets answers from rules to specific questions or queries. Use this tool whenever you need to look up game rules, mechanics, or rule clarifications. The tool will search through the indexed rules for the current scenario's ruleset and return relevant information.")]
    public async Task<string> SearchRulesAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return "Please provide a query or question about the rules.";
        }

        string rulesetId = _game.Ruleset.Id;
        
        return await _rulesSearchService.SearchRulesAsync(rulesetId, query);
    }
}

