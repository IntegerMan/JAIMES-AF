using System.ComponentModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.Logging;

namespace MattEland.Jaimes.Evaluators;

/// <summary>
/// An evaluator that scores a conversation based on how well the AI game master maintains narrative engagement,
/// guides the story forward, and keeps the experience compelling. This evaluator analyzes trends over recent
/// interactions rather than just the final response.
/// </summary>
/// <param name="chatClient">The chat client to use for evaluation.</param>
/// <param name="logger">The logger for telemetry instrumentation.</param>
[Description("Evaluates assistant responses for narrative engagement, pacing, tension, and storytelling quality across recent interactions.")]
public class StorytellerEvaluator(IChatClient chatClient, ILogger<StorytellerEvaluator> logger) : LlmBasedEvaluator(chatClient, logger)
{
    /// <summary>
    /// The name of the metric produced by this evaluator.
    /// </summary>
    public const string MetricName = "Storyteller";

    private const int TargetExchangeCount = 5;
    private const int MinimumExchangeWarningThreshold = 3;

    /// <inheritdoc />
    public override string EvaluatorMetricName => MetricName;

    /// <inheritdoc />
    public override async ValueTask<EvaluationResult> EvaluateAsync(
        IEnumerable<ChatMessage> messages,
        ChatResponse modelResponse,
        ChatConfiguration? chatConfiguration = null,
        IEnumerable<EvaluationContext>? evaluationContext = null,
        CancellationToken cancellationToken = default)
    {
        // Extract system prompt and conversation messages
        var (systemPrompt, conversationMessages) = ExtractMessages(messages);

        // Extract the last N exchanges (user + assistant pairs) for trend analysis
        List<(ChatMessage User, ChatMessage Assistant)> recentExchanges = ExtractRecentExchanges(conversationMessages, modelResponse, TargetExchangeCount);
        int exchangeCount = recentExchanges.Count;

        // Build conversation history text showing the exchanges
        string conversationHistory = BuildConversationHistory(recentExchanges);

        // Build the evaluation prompt
        string prompt = BuildEvaluationPrompt(
            modelResponse.Text ?? string.Empty,
            conversationHistory,
            systemPrompt,
            exchangeCount);

        string responseText = await GetEvaluationResponseAsync(prompt, cancellationToken);

        // Parse the response
        EvaluationParseResult parseResult = ParseEvaluationResponse(responseText);

        // Create metric with standard diagnostics
        NumericMetric metric = CreateMetric(parseResult, responseText);

        // Add evaluator-specific diagnostics
        if (parseResult.ParseSuccess)
        {
            metric.Diagnostics!.Add(new EvaluationDiagnostic(
                EvaluationDiagnosticSeverity.Informational,
                $"Exchanges analyzed: {exchangeCount}"));
        }

        // Add warning diagnostic if conversation is too short for reliable trend analysis
        if (exchangeCount < MinimumExchangeWarningThreshold)
        {
            metric.Diagnostics!.Add(new EvaluationDiagnostic(
                EvaluationDiagnosticSeverity.Warning,
                $"Limited conversation context: Only {exchangeCount} exchange(s) available for trend analysis. Results may be less reliable."));
        }

        return new EvaluationResult(metric);
    }

    private static List<(ChatMessage User, ChatMessage Assistant)> ExtractRecentExchanges(
        List<ChatMessage> conversationMessages,
        ChatResponse modelResponse,
        int maxExchanges)
    {
        var exchanges = new List<(ChatMessage User, ChatMessage Assistant)>();

        // Find all User messages and pair them with the message that follows (if it's an Assistant)
        // or the modelResponse (if it's the final User message)
        for (int i = 0; i < conversationMessages.Count; i++)
        {
            if (conversationMessages[i].Role == ChatRole.User)
            {
                ChatMessage userMsg = conversationMessages[i];
                
                // If there's a following message in conversationMessages, and it's Assistant, use it
                if (i + 1 < conversationMessages.Count && conversationMessages[i + 1].Role == ChatRole.Assistant)
                {
                    exchanges.Add((userMsg, conversationMessages[i + 1]));
                }
                // Otherwise, if it's the very last User message in the whole context, use the modelResponse
                else if (i == conversationMessages.FindLastIndex(m => m.Role == ChatRole.User))
                {
                    ChatMessage assistantMsg = modelResponse.Messages.FirstOrDefault(m => m.Role == ChatRole.Assistant) 
                                                ?? new ChatMessage(ChatRole.Assistant, modelResponse.Text ?? string.Empty);
                    exchanges.Add((userMsg, assistantMsg));
                }
            }
        }

        // Take the most recent exchanges
        return exchanges
            .TakeLast(maxExchanges)
            .ToList();
    }

    private static string BuildConversationHistory(List<(ChatMessage User, ChatMessage Assistant)> exchanges)
    {
        if (exchanges.Count == 0)
        {
            return "No conversation exchanges available.";
        }

        var parts = new List<string>();
        int exchangeNumber = 1;

        foreach (var (user, assistant) in exchanges)
        {
            parts.Add($"--- Exchange {exchangeNumber} ---");
            parts.Add($"Player: {user.Text}");
            parts.Add($"Game Master: {assistant.Text}");
            exchangeNumber++;
        }

        return string.Join("\n", parts);
    }

    private static string BuildEvaluationPrompt(
        string responseText,
        string conversationHistory,
        string? systemPrompt,
        int exchangeCount)
    {
        string exchangeContext = exchangeCount switch
        {
            0 => "Note: No prior exchanges are available. Evaluate based on the current response only.",
            1 => "Note: Only 1 exchange is available. Limited trend analysis is possible.",
            < 3 => $"Note: Only {exchangeCount} exchanges are available. Trend analysis is limited.",
            _ => $"Analyzing the last {exchangeCount} exchanges for narrative trends."
        };

        return $"""
            You are an expert in evaluating the quality of storytelling from an AI Game Master in a solo role-playing game. Your goal is to assess how well the Game Master maintains narrative engagement, guides the story forward, and keeps the experience compelling for the player.

            IMPORTANT: You are evaluating TRENDS across the conversation, not just the final response. Consider how the narrative has developed over the recent exchanges.

            Definition: You are given a definition of storytelling quality criteria to guide your evaluation.
            Data: Your input data includes the recent conversation exchanges between the player and Game Master.
            Tasks: Evaluate the overall storytelling quality based on the conversation trends.

            {exchangeContext}

            Definition
            Storytelling Quality refers to the AI Game Master's ability to maintain narrative engagement, create meaningful tension, guide the story at an appropriate pace, and provide the player with compelling hooks to pursue. A skilled Game Master balances challenge with fairness, moves the story forward without rushing, and gives players clear opportunities to focus on elements they find interesting.

            Evaluation Criteria
            Consider these aspects when evaluating storytelling quality:

            1. NARRATIVE MOMENTUM: Does the story progress meaningfully? Are there clear developments, revelations, or consequences that move the narrative forward? A stalling narrative that goes nowhere loses player interest.

            2. TENSION & CHALLENGE: Are there meaningful stakes that engage the player? Good storytelling includes obstacles, conflicts, and challenges that make success feel earned. However, challenges should be fair and surmountable.

            3. FAIRNESS: Is the player given reasonable opportunities to succeed? Does the GM acknowledge player actions and give them meaningful impact? Unfair scenarios where the player has no real chance to succeed are frustrating.

            4. PACING: Is the narrative moving at an appropriate speed? Rushing through content doesn't give players time to engage with what interests them. Dragging creates boredom. Good pacing lets players savor important moments while maintaining forward momentum.

            5. FOCUS OPPORTUNITIES: Does the player have clear hooks and options to pursue? Good storytelling presents interesting threads, characters, mysteries, or goals that invite player investment without dictating what they must do.

            Ratings
            [Storyteller: 1] (Disengaging Narrative)
            Definition: The storytelling fails to engage the player. The narrative is stagnant with no meaningful progression, there are no stakes or tension, the GM is unfair to the player, pacing is severely off (either nothing happens or events rush by without player input), and there are no clear hooks for player interest.

            Examples:
            - Multiple exchanges where nothing meaningful happens
            - The player's actions have no consequences or are ignored
            - Overwhelming the player with rapid-fire events they cannot respond to
            - Situations that are clearly unwinnable or punishingly unfair

            [Storyteller: 2] (Weak Narrative)
            Definition: The storytelling has significant issues. There may be some narrative progression but it feels forced or uninteresting. Tension is minimal or artificially inflated. The pacing has notable problems. The player has few meaningful options to pursue.

            Examples:
            - Story progression that feels disconnected from player actions
            - Stakes that feel arbitrary or unearned
            - Long stretches of description with little happening
            - Limited player agency in shaping the narrative direction

            [Storyteller: 3] (Adequate Narrative)
            Definition: The storytelling is functional but unremarkable. The story moves forward but without strong hooks. There is some tension but it doesn't fully engage. Pacing is acceptable but may drag or rush at times. The player has options but they may not feel compelling.

            Examples:
            - Competent but predictable story progression
            - Some challenge present but stakes feel low
            - Adequate but uninspiring descriptions and events
            - Options exist but don't strongly invite investment

            [Storyteller: 4] (Engaging Narrative)
            Definition: The storytelling is good with engaging elements. The narrative progresses with interesting developments. There is meaningful tension and challenge while remaining fair. Pacing is generally appropriate. The player has clear and interesting hooks to pursue.

            Examples:
            - Story developments that feel consequential and connected to player actions
            - Challenges that are engaging without being unfair
            - Good balance of action, description, and player opportunity
            - Multiple interesting threads for the player to follow

            [Storyteller: 5] (Compelling Narrative)
            Definition: The storytelling is excellent and highly engaging. The narrative has strong forward momentum with exciting developments. Tension and stakes feel real and meaningful while giving the player fair chances. Pacing is excellent, giving weight to important moments without dragging. The player has multiple compelling hooks that invite deep investment.

            Examples:
            - Narrative developments that create genuine excitement or intrigue
            - Challenges that feel earned and satisfying to overcome
            - Perfect pacing that matches the story's emotional beats
            - Rich world and character hooks that make the player want to explore more

            Data
            MOST RECENT RESPONSE: [Game Master] {responseText}

            RECENT CONVERSATION EXCHANGES:
            {conversationHistory}

            {(string.IsNullOrWhiteSpace(systemPrompt) ? "" : $"\nSystem Instructions for the Game Master:\n{systemPrompt}")}

            Tasks
            Please provide your assessment Score based on the storytelling quality observed across the conversation exchanges. Your output should include the following information:
            ThoughtChain: To improve the reasoning process, think step by step and include a step-by-step explanation of your thought process as you analyze the conversation trends based on the definitions. Consider narrative momentum, tension, fairness, pacing, and focus opportunities across all exchanges. Keep it brief and start your ThoughtChain with "Let's think step by step:".
            Explanation: a very short explanation of why you think the conversation should get that Score.
            Score: based on your previous analysis, provide your Score. The Score you give MUST be an integer score (i.e., "1", "2"...) based on the levels of the definitions.
            Please provide your answers between the tags: <S0>your chain of thoughts</S0>, <S1>your explanation</S1>, <S2>your Score</S2>.
            Output
            """;
    }
}
