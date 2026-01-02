using System.ComponentModel;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

namespace MattEland.Jaimes.Workers.AssistantMessageWorker.Services;

/// <summary>
/// An evaluator that scores a conversation based on how much the AI assistant respected the player's agency.
/// </summary>
/// <param name="chatClient">The chat client to use for evaluation.</param>
[Description("Evaluates assistant responses for how well they preserve player agency by avoiding making decisions on the player's behalf or presenting restrictive choice lists.")]
public class PlayerAgencyEvaluator(IChatClient chatClient) : IEvaluator
{
    public const string MetricName = "PlayerAgency";

    public IReadOnlyCollection<string> EvaluationMetricNames => [MetricName];

    public async ValueTask<EvaluationResult> EvaluateAsync(
        IEnumerable<ChatMessage> messages,
        ChatResponse modelResponse,
        ChatConfiguration? chatConfiguration = null,
        IEnumerable<EvaluationContext>? evaluationContext = null,
        CancellationToken cancellationToken = default)
    {
        // Extract system prompt and conversation messages
        List<ChatMessage> messagesList = messages.ToList();
        string? systemPrompt = messagesList.FirstOrDefault(m => m.Role == ChatRole.System)?.Text;
        List<ChatMessage> conversationMessages = messagesList
            .Where(m => m.Role != ChatRole.System)
            .ToList();

        // Build conversation history text
        string conversationHistory = string.Join("\n", conversationMessages.Select(m => $"{m.Role}: {m.Text}"));

        // Build the Fluency-style prompt
        string prompt = $"""
                         You are an expert in evaluating the quality of a RESPONSE from an intelligent system based on provided definition and data. Your goal will involve answering the questions below using the information provided.
                         Definition: You are given a definition of the communication trait that is being evaluated to help guide your Score.
                         Data: Your input data include a RESPONSE.
                         Tasks: To complete your evaluation you will be asked to evaluate the Data in different ways.

                         Definition
                         Player Agency refers to the degree to which an AI Game Master respects the player's autonomy and decision-making authority in a role-playing game context. A response that preserves player agency allows the player to make their own choices, avoids making decisions on the player's behalf, and refrains from presenting restrictive multiple-choice lists that limit player creativity. The AI should describe the world and consequences of actions without assuming what the player will do or forcing them into predetermined paths.

                         Ratings
                         [PlayerAgency: 1] (No Agency Respect)
                         Definition: The response completely fails to respect player agency. It makes major assumptions about what the player will do, takes actions on the player's behalf, or presents a restrictive list of choices that severely limits player autonomy. The response treats the player as a passive observer rather than an active decision-maker.

                         Examples: 
                         Response: "You decide to open the door and enter the room. Inside, you see a treasure chest. You open it and find gold coins."

                         Response: "What would you like to do? A) Go north B) Go south C) Attack the guard D) Talk to the merchant"

                         [PlayerAgency: 2] (Minimal Agency Respect)
                         Definition: The response has significant issues with agency. It frequently makes assumptions about player actions, takes some decisions for the player, or presents overly restrictive choice lists. The player's sense of control and autonomy is substantially diminished.

                         Examples:
                         Response: "You walk down the corridor and notice a hidden passage. You investigate it and find a secret room."

                         Response: "You can either: 1) Fight the dragon 2) Run away 3) Try to negotiate"

                         [PlayerAgency: 3] (Partial Agency Respect)
                         Definition: The response has some issues with agency. It may occasionally make minor assumptions, suggest a list of possible actions (which limits creativity), or slightly overstep by describing what the player does rather than what happens. The response communicates the situation but doesn't fully preserve player autonomy.

                         Examples:
                         Response: "The door creaks open. You step inside and look around the dimly lit chamber."

                         Response: "You might want to consider: searching the room, talking to the NPC, or examining the artifact."

                         [PlayerAgency: 4] (Good Agency Respect)
                         Definition: The response respects player agency with only minor issues. It generally allows the player to make their own decisions and describes the world and consequences without taking actions for the player. There may be occasional minor assumptions or suggestions, but they don't significantly impact player autonomy. A common issue at this level is presenting choices in a question format like "Would you like to X, Y, or Z?" which, while less restrictive than numbered lists, still limits player creativity by suggesting specific options rather than allowing open-ended exploration.

                         Examples:
                         Response: "The door stands before you, slightly ajar. A faint light spills from the crack. The air carries a musty scent, and you hear distant sounds echoing from within."

                         Response: "The merchant eyes you warily as you approach. His hand moves toward a hidden weapon, but he hasn't drawn it yet. The marketplace bustles around you, and other shoppers seem to be watching the interaction."

                         Response: "The path splits ahead. Would you like to follow the left trail into the forest, take the right path toward the mountains, or investigate the strange markings on the ground?"

                         Response: "You've reached the tavern. Would you like to order a drink, talk to the bartender, or look for information about the quest?"

                         [PlayerAgency: 5] (Exceptional Agency Respect)
                         Definition: The response perfectly respects player agency. It describes the world, situations, and consequences without making any assumptions about what the player will do. It never takes actions on the player's behalf or presents restrictive choice lists. The player feels fully in control of their character's decisions and actions.

                         Examples:
                         Response: "The ancient door stands before you, its surface carved with runes that seem to shift in the torchlight. The air grows colder as you approach, and you notice the lock mechanism appears to be of dwarven craftsmanship."

                         Response: "The marketplace is alive with activity. Vendors call out their wares, children dart between stalls, and guards patrol the perimeter. The merchant you've been tracking sets up his display, carefully arranging his goods while keeping a watchful eye on the crowd."

                         Data
                         RESPONSE: [assistant] {modelResponse.Text}

                         Conversation Context:
                         {conversationHistory}

                         {(string.IsNullOrWhiteSpace(systemPrompt) ? "" : $"\nSystem Instructions:\n{systemPrompt}")}

                         Tasks
                         Please provide your assessment Score for the previous RESPONSE based on the Definitions above. Your output should include the following information:
                         ThoughtChain: To improve the reasoning process, think step by step and include a step-by-step explanation of your thought process as you analyze the data based on the definitions. Keep it brief and start your ThoughtChain with "Let's think step by step:".
                         Explanation: a very short explanation of why you think the input Data should get that Score.
                         Score: based on your previous analysis, provide your Score. The Score you give MUST be a integer score (i.e., "1", "2"...) based on the levels of the definitions.
                         Please provide your answers between the tags: <S0>your chain of thoughts</S0>, <S1>your explanation</S1>, <S2>your Score</S2>.
                         Output
                         """;

        ChatOptions chatOptions = new()
        {
            Tools = [] // Ensure no tools are used
        };

        var requestMessages = new List<ChatMessage>
        {
            new(ChatRole.User, prompt)
        };

        ChatResponse response = await chatClient.GetResponseAsync(requestMessages, chatOptions, cancellationToken);
        string responseText = response.Text;

        // Parse the response for S0, S1, S2 tags
        int score = 1;
        string thoughtChain = string.Empty;
        string explanation = "Failed to parse evaluation response.";
        bool parseSuccess = false;

        if (!string.IsNullOrWhiteSpace(responseText))
        {
            const RegexOptions regexOptions = RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture;
            TimeSpan regexTimeout = TimeSpan.FromSeconds(1);

            try
            {
                // Extract S0 (ThoughtChain)
                Match s0Match = Regex.Match(responseText, @"<S0>(?<content>.*?)</S0>", regexOptions, regexTimeout);
                if (s0Match.Success)
                {
                    thoughtChain = s0Match.Groups["content"].Value.Trim();
                }

                // Extract S1 (Explanation)
                Match s1Match = Regex.Match(responseText, @"<S1>(?<content>.*?)</S1>", regexOptions, regexTimeout);
                if (s1Match.Success)
                {
                    explanation = s1Match.Groups["content"].Value.Trim();
                }

                // Extract S2 (Score)
                Match s2Match = Regex.Match(responseText, @"<S2>(?<content>.*?)</S2>", regexOptions, regexTimeout);
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
                $"Player Agency Score: {score} ({passFailStatus})"));
            
            if (!string.IsNullOrWhiteSpace(thoughtChain))
            {
                metric.Diagnostics.Add(new EvaluationDiagnostic(
                    EvaluationDiagnosticSeverity.Informational,
                    $"ThoughtChain: {thoughtChain}"));
            }
        }
        else
        {
            metric.Diagnostics.Add(new EvaluationDiagnostic(
                EvaluationDiagnosticSeverity.Warning,
                $"Failed to parse evaluation response. Raw response: {responseText.Substring(0, Math.Min(200, responseText.Length))}"));
        }

        return new EvaluationResult(metric);
    }
}
