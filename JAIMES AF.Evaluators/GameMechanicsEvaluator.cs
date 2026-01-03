using System.ComponentModel;
using System.Text.RegularExpressions;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

namespace MattEland.Jaimes.Evaluators;

/// <summary>
/// An evaluator that scores a conversation based on how well the AI game master follows game mechanics
/// and rules from the specified ruleset.
/// </summary>
/// <param name="chatClient">The chat client to use for evaluation.</param>
/// <param name="rulesSearchService">The rules search service to find relevant game rules.</param>
[Description("Evaluates assistant responses for adherence to game mechanics and rules from the specified ruleset.")]
public class GameMechanicsEvaluator(IChatClient chatClient, IRulesSearchService rulesSearchService) : IEvaluator
{
    public const string MetricName = "GameMechanics";

    public IReadOnlyCollection<string> EvaluationMetricNames => [MetricName];

    public async ValueTask<EvaluationResult> EvaluateAsync(
        IEnumerable<ChatMessage> messages,
        ChatResponse modelResponse,
        ChatConfiguration? chatConfiguration = null,
        IEnumerable<EvaluationContext>? evaluationContext = null,
        CancellationToken cancellationToken = default)
    {
        // Find the GameMechanicsEvaluationContext from the evaluation contexts
        GameMechanicsEvaluationContext? mechanicsContext = evaluationContext?
            .OfType<GameMechanicsEvaluationContext>()
            .FirstOrDefault();

        string? rulesetId = mechanicsContext?.RulesetId;
        string? rulesetName = mechanicsContext?.RulesetName ?? "All Rulesets";
        string responseText = modelResponse.Text ?? string.Empty;

        // Track if we're running without context
        bool runningWithoutContext = mechanicsContext == null;

        // Extract conversation context
        List<ChatMessage> messagesList = messages.ToList();
        string? systemPrompt = messagesList.FirstOrDefault(m => m.Role == ChatRole.System)?.Text;
        List<ChatMessage> conversationMessages = messagesList
            .Where(m => m.Role != ChatRole.System)
            .ToList();

        string conversationHistory = string.Join("\n", conversationMessages.Select(m => $"{m.Role}: {m.Text}"));

        // First, identify mechanics-related topics in the AI response
        List<string> mechanicsTopics = await IdentifyMechanicsTopicsAsync(responseText, conversationHistory, cancellationToken);

        // Search for relevant rules based on the identified topics
        List<SearchRuleResult> relevantRules = [];
        foreach (string topic in mechanicsTopics)
        {
            try
            {
                SearchRulesResponse searchResponse = await rulesSearchService.SearchRulesDetailedAsync(
                    rulesetId,
                    topic,
                    storeResults: false,
                    cancellationToken);

                relevantRules.AddRange(searchResponse.Results);
            }
            catch (Exception ex)
            {
                // Log but continue - we may still be able to evaluate with other rules
                // In production, this would use proper logging
                System.Diagnostics.Debug.WriteLine($"Failed to search rules for topic '{topic}': {ex.Message}");
            }
        }

        // Deduplicate rules by EmbeddingId
        relevantRules = relevantRules
            .GroupBy(r => r.EmbeddingId)
            .Select(g => g.OrderByDescending(r => r.Relevancy).First())
            .OrderByDescending(r => r.Relevancy)
            .Take(10) // Limit to top 10 most relevant rules
            .ToList();

        // Build the evaluation prompt
        string rulesContext = relevantRules.Count > 0
            ? string.Join("\n\n", relevantRules.Select(r => $"[{r.DocumentName}] {r.Text}"))
            : "No specific rules found for this context.";

        string prompt = BuildEvaluationPrompt(responseText, conversationHistory, systemPrompt, rulesContext, rulesetName);

        ChatOptions chatOptions = new()
        {
            Tools = [] // Ensure no tools are used
        };

        var requestMessages = new List<ChatMessage>
        {
            new(ChatRole.User, prompt)
        };

        ChatResponse response = await chatClient.GetResponseAsync(requestMessages, chatOptions, cancellationToken);
        string evaluationResponseText = response.Text;

        // Parse the response for S0, S1, S2 tags
        int score = 1;
        string thoughtChain = string.Empty;
        string explanation = "Failed to parse evaluation response.";
        bool parseSuccess = false;

        if (!string.IsNullOrWhiteSpace(evaluationResponseText))
        {
            const RegexOptions regexOptions = RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture;
            TimeSpan regexTimeout = TimeSpan.FromSeconds(1);

            try
            {
                // Extract S0 (ThoughtChain)
                Match s0Match = Regex.Match(evaluationResponseText, @"<S0>(?<content>.*?)</S0>", regexOptions, regexTimeout);
                if (s0Match.Success)
                {
                    thoughtChain = s0Match.Groups["content"].Value.Trim();
                }

                // Extract S1 (Explanation)
                Match s1Match = Regex.Match(evaluationResponseText, @"<S1>(?<content>.*?)</S1>", regexOptions, regexTimeout);
                if (s1Match.Success)
                {
                    explanation = s1Match.Groups["content"].Value.Trim();
                }

                // Extract S2 (Score)
                Match s2Match = Regex.Match(evaluationResponseText, @"<S2>(?<content>.*?)</S2>", regexOptions, regexTimeout);
                if (s2Match.Success)
                {
                    string scoreText = s2Match.Groups["content"].Value.Trim();
                    if (int.TryParse(scoreText, out int parsedScore))
                    {
                        score = parsedScore;
                        parseSuccess = true;
                    }
                }
            }
            catch (RegexMatchTimeoutException)
            {
                explanation = "Regex parsing timed out. Response may be malformed.";
            }
        }

        // Ensure score is within range
        score = Math.Clamp(score, 1, 5);

        // Determine pass/fail (fail on 3 or below, pass on 4 or 5)
        bool passed = score >= 4;
        string passFailStatus = passed ? "Pass" : "Fail";

        NumericMetric metric = new(MetricName)
        {
            Value = score,
            Reason = explanation
        };

        // Add comprehensive diagnostics
        metric.Diagnostics ??= [];

        if (parseSuccess)
        {
            metric.Diagnostics.Add(new EvaluationDiagnostic(
                EvaluationDiagnosticSeverity.Informational,
                $"Game Mechanics Score: {score} ({passFailStatus})"));

            if (!string.IsNullOrWhiteSpace(thoughtChain))
            {
                metric.Diagnostics.Add(new EvaluationDiagnostic(
                    EvaluationDiagnosticSeverity.Informational,
                    $"ThoughtChain: {thoughtChain}"));
            }

            metric.Diagnostics.Add(new EvaluationDiagnostic(
                EvaluationDiagnosticSeverity.Informational,
                $"Rules searched: {relevantRules.Count} rules found for {mechanicsTopics.Count} topics"));

            if (mechanicsTopics.Count > 0)
            {
                metric.Diagnostics.Add(new EvaluationDiagnostic(
                    EvaluationDiagnosticSeverity.Informational,
                    $"Mechanics topics identified: {string.Join(", ", mechanicsTopics)}"));
            }
        }
        else
        {
            string rawResponseText = string.IsNullOrWhiteSpace(evaluationResponseText) ? "{Empty}" : evaluationResponseText;
            int length = Math.Min(200, rawResponseText.Length);

            metric.Diagnostics.Add(new EvaluationDiagnostic(
                EvaluationDiagnosticSeverity.Warning,
                $"Failed to parse evaluation response. Raw response: {rawResponseText[..length]}"));
        }

        // Add diagnostic if running without ruleset context
        if (runningWithoutContext)
        {
            metric.Diagnostics.Add(new EvaluationDiagnostic(
                EvaluationDiagnosticSeverity.Warning,
                "No GameMechanicsEvaluationContext provided. Searching all rulesets for relevant rules."));
        }

        // Add context about the ruleset used (if provided)
        metric.Context ??= new Dictionary<string, EvaluationContext>();
        if (mechanicsContext != null)
        {
            metric.Context[GameMechanicsEvaluationContext.ContextName] = mechanicsContext;
        }

        return new EvaluationResult(metric);
    }

    private async Task<List<string>> IdentifyMechanicsTopicsAsync(
        string responseText,
        string conversationHistory,
        CancellationToken cancellationToken)
    {
        string topicPrompt = $"""
            You are an expert at identifying game mechanics and rules topics in role-playing game content.
            
            Analyze the following AI game master response and identify specific game mechanics or rules topics that should be verified against a ruleset. Focus on:
            - Combat mechanics (attacks, damage, hit points, armor, etc.)
            - Skill checks and dice rolls
            - Character abilities and powers
            - Movement and positioning
            - Magic and spells
            - Equipment and item effects
            - Status effects and conditions
            - Resource management (mana, stamina, etc.)
            - Turn order and action economy
            
            Only identify topics where the AI response makes specific claims about how mechanics work.
            If the response is purely narrative with no mechanical claims, respond with "NONE".
            
            AI Response to analyze:
            {responseText}
            
            Recent conversation context:
            {conversationHistory}
            
            List the mechanics topics, one per line. Do not include explanations. Just the topic keywords.
            Example format:
            damage calculation
            spell casting rules
            stealth checks
            """;

        ChatOptions chatOptions = new() { Tools = [] };
        var requestMessages = new List<ChatMessage> { new(ChatRole.User, topicPrompt) };

        ChatResponse response = await chatClient.GetResponseAsync(requestMessages, chatOptions, cancellationToken);
        string topicsText = response.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(topicsText) || topicsText.Trim().Equals("NONE", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        // Parse topics from response
        List<string> topics = topicsText
            .Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t) && !t.Equals("NONE", StringComparison.OrdinalIgnoreCase))
            .Take(5) // Limit to 5 topics to avoid too many searches
            .ToList();

        return topics;
    }

    private static string BuildEvaluationPrompt(
        string responseText,
        string conversationHistory,
        string? systemPrompt,
        string rulesContext,
        string? rulesetName)
    {
        string rulesetDescription = string.IsNullOrWhiteSpace(rulesetName)
            ? "the game's ruleset"
            : $"the {rulesetName} ruleset";

        return $"""
            You are an expert in evaluating the quality of a RESPONSE from an AI Game Master based on adherence to game mechanics and rules. Your goal is to evaluate how accurately the AI follows {rulesetDescription}.
            
            Definition: You are given a definition of the evaluation criteria and relevant rules from the game's ruleset.
            Data: Your input data includes the AI RESPONSE, conversation context, and relevant rules.
            Tasks: Evaluate how well the response follows the established game mechanics.

            Definition
            Game Mechanics Adherence refers to how accurately an AI Game Master follows the established rules and mechanics of the game system being played. A response that demonstrates good mechanics adherence correctly applies game rules for combat, skill checks, abilities, spells, and other mechanical elements. The AI should use proper terminology, apply correct modifiers, and follow the ruleset's procedures for resolving actions.

            IMPORTANT NOTES:
            - If the response is purely narrative without mechanical claims, it should still receive a high score (4-5) as there are no mechanics to violate.
            - Minor creative interpretations that don't contradict core rules are acceptable.
            - The focus is on preventing clear rule violations that would negatively impact gameplay.

            Ratings
            [GameMechanics: 1] (Severe Rule Violations)
            Definition: The response contains major violations of game mechanics. It fundamentally misapplies core rules, uses completely wrong procedures for resolving actions, or introduces mechanics that directly contradict the ruleset. These errors would significantly impact gameplay and player understanding of the game system.

            Examples:
            - Applying damage before an attack roll is made
            - Allowing impossible actions according to the rules
            - Completely misrepresenting how a core mechanic works

            [GameMechanics: 2] (Significant Rule Errors)
            Definition: The response has notable mechanical errors that deviate from the ruleset. It may use incorrect modifiers, misapply conditions or effects, or use wrong terminology for game concepts. These errors could confuse players about how the game works.

            Examples:
            - Using wrong damage dice for a weapon or spell
            - Misapplying advantage/disadvantage or similar mechanics
            - Forgetting important restrictions on abilities

            [GameMechanics: 3] (Minor Rule Deviations)
            Definition: The response has minor mechanical issues or ambiguities. The core mechanics are mostly correct, but there may be small inaccuracies in modifiers, timing, or secondary effects. The gameplay is not significantly impacted.

            Examples:
            - Slightly incorrect modifier calculations
            - Minor timing issues with effects
            - Using slightly imprecise terminology

            [GameMechanics: 4] (Mostly Accurate Mechanics)
            Definition: The response correctly applies game mechanics with only very minor issues or creative interpretations that don't violate rules. The AI demonstrates good understanding of the game system and applies rules appropriately.

            Examples:
            - Correctly resolving combat with proper procedures
            - Accurately describing spell effects and limitations
            - Making reasonable rulings where rules are ambiguous

            [GameMechanics: 5] (Perfect Mechanics Adherence)
            Definition: The response perfectly follows game mechanics and rules. It uses correct terminology, applies proper modifiers and procedures, and demonstrates expert knowledge of the game system. Any rulings made are consistent with the spirit of the rules. Purely narrative responses with no mechanical claims also qualify for this score.

            Examples:
            - Flawlessly executing complex combat scenarios
            - Correctly applying all relevant modifiers and conditions
            - Using precise game terminology throughout

            Relevant Rules from the Ruleset
            {rulesContext}

            Data
            RESPONSE: [assistant] {responseText}

            Conversation Context:
            {conversationHistory}

            {(string.IsNullOrWhiteSpace(systemPrompt) ? "" : $"\nSystem Instructions:\n{systemPrompt}")}

            Tasks
            Please provide your assessment Score for the previous RESPONSE based on the Definitions above and the relevant rules provided. Your output should include the following information:
            ThoughtChain: To improve the reasoning process, think step by step and include a step-by-step explanation of your thought process as you analyze the data based on the definitions. Consider what mechanical claims (if any) are made in the response and compare them to the rules. Keep it brief and start your ThoughtChain with "Let's think step by step:".
            Explanation: a very short explanation of why you think the input Data should get that Score.
            Score: based on your previous analysis, provide your Score. The Score you give MUST be an integer score (i.e., "1", "2"...) based on the levels of the definitions.
            Please provide your answers between the tags: <S0>your chain of thoughts</S0>, <S1>your explanation</S1>, <S2>your Score</S2>.
            Output
            """;
    }
}
