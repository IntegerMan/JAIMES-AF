using System.Diagnostics;
using System.Text;
using System.Text.Json;
using MattEland.Jaimes.Agents.Helpers;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MattEland.Jaimes.Agents.ContextProviders;

/// <summary>
/// An AIContextProvider that automatically injects relevant rules context before each agent invocation.
/// Uses a lightweight LLM call to extract search queries from the conversation, then retrieves relevant rules.
/// </summary>
public class RulesTextSearchProvider : AIContextProvider
{
    private static readonly ActivitySource ActivitySource = new("Jaimes.Agents.RulesSearch");

    private readonly string _rulesetId;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RulesTextSearchProvider> _logger;
    private readonly IConfiguration _configuration;

    // Store the last user query for context injection
    private string? _lastUserQuery;

    /// <summary>
    /// Creates a new RulesTextSearchProvider for the given ruleset.
    /// </summary>
    public RulesTextSearchProvider(
        string rulesetId,
        IServiceProvider serviceProvider,
        ILogger<RulesTextSearchProvider> logger,
        IConfiguration configuration)
    {
        _rulesetId = rulesetId ?? throw new ArgumentNullException(nameof(rulesetId));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Creates a provider from serialized state (used when deserializing threads).
    /// </summary>
    public RulesTextSearchProvider(
        string rulesetId,
        IServiceProvider serviceProvider,
        ILogger<RulesTextSearchProvider> logger,
        IConfiguration configuration,
        JsonElement serializedState,
        JsonSerializerOptions? jsonSerializerOptions = null)
        : this(rulesetId, serviceProvider, logger, configuration)
    {
        // Restore last query from serialized state if available
        if (serializedState.ValueKind == JsonValueKind.Object &&
            serializedState.TryGetProperty("LastUserQuery", out JsonElement queryElement))
        {
            _lastUserQuery = queryElement.GetString();
        }
    }

    /// <summary>
    /// Sets the current user query for context injection.
    /// Called by the agent before invoking the LLM.
    /// </summary>
    public void SetUserQuery(string? query)
    {
        _lastUserQuery = query;
    }

    /// <summary>
    /// Called before the agent invokes the LLM. Extracts search queries and injects relevant rules context.
    /// </summary>
    public override async ValueTask<AIContext> InvokingAsync(InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = ActivitySource.StartActivity("RulesSearch.ContextInjection");
        activity?.SetTag("ruleset.id", _rulesetId);

        try
        {
            // Use the stored user query if no direct access to messages
            string? userQuery = _lastUserQuery;

            if (string.IsNullOrWhiteSpace(userQuery))
            {
                _logger.LogDebug("No user query available for rules search context injection");
                return new AIContext();
            }

            // Extract search queries using lightweight LLM
            List<string> queries = await ExtractSearchQueriesAsync(userQuery, cancellationToken);
            if (queries.Count == 0)
            {
                _logger.LogDebug("No search queries extracted from user message");
                return new AIContext();
            }

            activity?.SetTag("search.query_count", queries.Count);
            _logger.LogInformation("Extracted {QueryCount} search queries for rules context", queries.Count);

            // Search for each query and collect results
            List<SearchRuleResult> allResults = [];

            using IServiceScope scope = _serviceProvider.CreateScope();
            IRulesSearchService? rulesSearchService = scope.ServiceProvider.GetService<IRulesSearchService>();

            if (rulesSearchService == null)
            {
                _logger.LogWarning("IRulesSearchService not available - skipping rules context injection");
                return new AIContext();
            }

            foreach (string query in queries)
            {
                try
                {
                    // Search with storeResults=true to ensure RAG storage logging
                    SearchRulesResponse response = await rulesSearchService.SearchRulesDetailedAsync(
                        _rulesetId,
                        query,
                        storeResults: true,
                        cancellationToken);

                    // Take top 3 per query
                    allResults.AddRange(response.Results.Take(3));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to search rules for query: {Query}", query);
                }
            }

            if (allResults.Count == 0)
            {
                _logger.LogDebug("No rules found for extracted queries");
                return new AIContext();
            }

            // Deduplicate by ChunkId (in case multiple queries return the same rule)
            List<SearchRuleResult> uniqueResults = allResults
                .GroupBy(r => r.ChunkId)
                .Select(g => g.First())
                .OrderByDescending(r => r.Relevancy)
                .Take(9) // Max 9 unique results (3 queries * 3 results, but may overlap)
                .ToList();

            activity?.SetTag("search.result_count", uniqueResults.Count);
            _logger.LogInformation("Injecting {ResultCount} rules as context", uniqueResults.Count);

            // Format rules as context
            string rulesContext = FormatRulesContext(uniqueResults);

            return new AIContext
            {
                Instructions = rulesContext
            };
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Error during rules context injection");
            return new AIContext(); // Don't break the agent if context injection fails
        }
    }

    /// <summary>
    /// Extracts one or more search queries from the user message using a lightweight LLM call.
    /// </summary>
    private async Task<List<string>> ExtractSearchQueriesAsync(string userMessage, CancellationToken cancellationToken)
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        IChatClient? chatClient = scope.ServiceProvider.GetService<IChatClient>();

        if (chatClient == null)
        {
            _logger.LogWarning("IChatClient not available - using user message directly as query");
            return [userMessage];
        }

        // Wrap with instrumentation to respect AI:EnableSensitiveLogging configuration
        bool enableSensitiveLogging = bool.TryParse(_configuration["AI:EnableSensitiveLogging"], out bool val) && val;
        IChatClient instrumentedClient = chatClient.WrapWithInstrumentation(_logger, enableSensitiveLogging);

        try
        {
            string extractionPrompt = """
                                      You are a query extraction assistant. Given a user message from a tabletop RPG game, extract 1-3 search queries that would help find relevant game rules.

                                      Focus on:
                                      - Game mechanics mentioned (combat, spells, skills, abilities)
                                      - Specific rules or procedures being asked about
                                      - Character actions that might have rules associated

                                      Return ONLY the search queries, one per line. If the message doesn't seem to need any rules lookup, return nothing.

                                      User message: {0}
                                      """;

            ChatResponse response = await instrumentedClient.GetResponseAsync(
                string.Format(extractionPrompt, userMessage),
                new ChatOptions
                {
                    MaxOutputTokens = 100,
                    Temperature = 0.0f
                },
                cancellationToken);

            string? result = response.Text;
            if (string.IsNullOrWhiteSpace(result))
            {
                return [];
            }

            // Parse one query per line
            return result
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(q => !string.IsNullOrWhiteSpace(q) && q.Length > 2)
                .Take(3)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract search queries via LLM - using user message directly");
            return [userMessage];
        }
    }

    /// <summary>
    /// Formats the search results as context to inject into the agent's instructions.
    /// </summary>
    private static string FormatRulesContext(List<SearchRuleResult> results)
    {
        StringBuilder sb = new();
        sb.AppendLine();
        sb.AppendLine("## Relevant Rules Context");
        sb.AppendLine("The following rules may be relevant to the current situation:");
        sb.AppendLine();

        foreach (SearchRuleResult result in results)
        {
            sb.AppendLine("---");
            if (!string.IsNullOrWhiteSpace(result.DocumentName))
            {
                sb.AppendLine($"**Source**: {result.DocumentName}");
            }

            sb.AppendLine(result.Text);
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine("Use these rules to inform your response. Cite specific rules when applicable.");

        return sb.ToString();
    }

    /// <summary>
    /// Serializes the provider state for thread persistence.
    /// </summary>
    public override JsonElement Serialize(JsonSerializerOptions? jsonSerializerOptions = null)
    {
        return JsonSerializer.SerializeToElement(new
        {
            RulesetId = _rulesetId,
            LastUserQuery = _lastUserQuery
        }, jsonSerializerOptions);
    }
}
