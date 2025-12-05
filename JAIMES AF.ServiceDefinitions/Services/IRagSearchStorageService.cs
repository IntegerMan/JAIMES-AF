using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.ServiceDefinitions.Services;

/// <summary>
/// Service for storing RAG search queries and results for diagnostic and evaluation purposes.
/// Storage is performed asynchronously via a queue to avoid blocking the search operation.
/// </summary>
public interface IRagSearchStorageService
{
    /// <summary>
    /// Enqueues a search query and its results for asynchronous storage.
    /// This method returns immediately without waiting for storage to complete.
    /// </summary>
    /// <param name="query">The search query text</param>
    /// <param name="rulesetId">The ruleset ID filter applied (null if searching all rulesets)</param>
    /// <param name="indexName">The name of the index/collection searched</param>
    /// <param name="filterJson">JSON representation of any filters applied</param>
    /// <param name="results">The search results to store</param>
    void EnqueueSearchResults(string query, string? rulesetId, string indexName, string? filterJson, SearchRuleResult[] results);
}
