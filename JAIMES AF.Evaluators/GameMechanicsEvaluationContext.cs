using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

namespace MattEland.Jaimes.Evaluators;

/// <summary>
/// Provides context information for the <see cref="GameMechanicsEvaluator"/> to evaluate
/// whether AI game master responses correctly follow game ruleset mechanics.
/// </summary>
public class GameMechanicsEvaluationContext : EvaluationContext
{
    /// <summary>
    /// The name used to identify this context type.
    /// </summary>
    public const string ContextName = "GameMechanicsContext";

    /// <summary>
    /// Initializes a new instance of the <see cref="GameMechanicsEvaluationContext"/> class.
    /// </summary>
    /// <param name="rulesetId">The ID of the ruleset to evaluate against.</param>
    /// <param name="rulesetName">The optional display name of the ruleset.</param>
    public GameMechanicsEvaluationContext(string rulesetId, string? rulesetName = null)
        : base(ContextName, CreateContents(rulesetId, rulesetName))
    {
        RulesetId = rulesetId ?? throw new ArgumentNullException(nameof(rulesetId));
        RulesetName = rulesetName;
    }

    /// <summary>
    /// Gets the ID of the ruleset to evaluate game mechanics against.
    /// </summary>
    public string RulesetId { get; }

    /// <summary>
    /// Gets the optional display name of the ruleset.
    /// </summary>
    public string? RulesetName { get; }

    private static IEnumerable<AIContent> CreateContents(string rulesetId, string? rulesetName)
    {
        string description = string.IsNullOrWhiteSpace(rulesetName)
            ? $"Ruleset ID: {rulesetId}"
            : $"Ruleset: {rulesetName} (ID: {rulesetId})";

        yield return new TextContent(description);
    }
}
