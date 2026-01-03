namespace MattEland.Jaimes.Evaluators;

/// <summary>
/// Represents the parsed result from an LLM-based evaluation response.
/// Extracts structured data from the S0, S1, and S2 tags.
/// </summary>
/// <param name="Score">The numeric score (1-5) extracted from the S2 tag.</param>
/// <param name="ThoughtChain">The reasoning chain extracted from the S0 tag.</param>
/// <param name="Explanation">The explanation extracted from the S1 tag.</param>
/// <param name="ParseSuccess">Whether the response was successfully parsed.</param>
public record EvaluationParseResult(
    int Score,
    string ThoughtChain,
    string Explanation,
    bool ParseSuccess);
