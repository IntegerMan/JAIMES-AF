using System.ComponentModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

namespace MattEland.Jaimes.Evaluators;

/// <summary>
/// An evaluator that scores a conversation based on how much the AI assistant respected the player's agency.
/// </summary>
/// <param name="chatClient">The chat client to use for evaluation.</param>
[Description("Evaluates assistant responses for how well they preserve player agency by avoiding making decisions on the player's behalf or presenting restrictive choice lists.")]
public class PlayerAgencyEvaluator(IChatClient chatClient) : LlmBasedEvaluator(chatClient)
{
    /// <summary>
    /// The name of the metric produced by this evaluator.
    /// </summary>
    public const string MetricName = "PlayerAgency";

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

        // Build conversation history text
        string conversationHistory = BuildSimpleConversationHistory(conversationMessages);

        // Build the Fluency-style prompt
        string prompt = $"""
                         You are an expert in evaluating the quality of a RESPONSE from an intelligent system based on provided definition and data. Your goal will involve answering the questions below using the information provided.
                         Definition: You are given a definition of the communication trait that is being evaluated to help guide your Score.
                         Data: Your input data include a RESPONSE.
                         Tasks: To complete your evaluation you will be asked to evaluate the Data in different ways.

                         Definition
                         Player Agency refers to the degree to which an AI Game Master respects the player's autonomy and decision-making authority in a role-playing game context. A response that preserves player agency allows the player to make their own choices, avoids making decisions on the player's behalf, and refrains from presenting multiple-choice lists that limit player creativity. The AI should describe the world and consequences of actions without assuming what the player will do or forcing them into predetermined paths.

                         CRITICAL: Presenting numbered lists, bulleted lists, or explicit "menu" options (e.g., "Do you want to: 1) X, 2) Y, 3) Z") is a significant agency violation that should NEVER score above 4, regardless of how well-written the descriptive prose is. Even beautiful prose followed by a choice list is hand-holding the player and limiting their creativity.

                         Ratings
                         [PlayerAgency: 1] (No Agency Respect)
                         Definition: The response completely fails to respect player agency. It makes major assumptions about what the player will do, takes actions on the player's behalf, or presents a restrictive list of choices that severely limits player autonomy. The response treats the player as a passive observer rather than an active decision-maker.

                         Examples: 
                         Response: "You decide to open the door and enter the room. Inside, you see a treasure chest. You open it and find gold coins."

                         Response: "What would you like to do? A) Go north B) Go south C) Attack the guard D) Talk to the merchant"

                         [PlayerAgency: 2] (Minimal Agency Respect)
                         Definition: The response has significant issues with agency. It frequently makes assumptions about player actions, takes some decisions for the player, or presents overly restrictive choice lists with minimal description. The player's sense of control and autonomy is substantially diminished.

                         Examples:
                         Response: "You walk down the corridor and notice a hidden passage. You investigate it and find a secret room."

                         Response: "You can either: 1) Fight the dragon 2) Run away 3) Try to negotiate"

                         [PlayerAgency: 3] (Partial Agency Respect)
                         Definition: The response has notable issues with agency. It may have good descriptive prose BUT undermines it by presenting numbered/bulleted choice lists, asking the player to pick from explicit options, or making minor assumptions about player actions. The response communicates the situation but doesn't fully preserve player autonomy because it hand-holds the player with suggested actions.

                         Examples:
                         Response: "The door creaks open. You step inside and look around the dimly lit chamber."

                         Response: "You might want to consider: searching the room, talking to the NPC, or examining the artifact."

                         Response: "[Beautiful descriptive prose about a magical tree]... What do you wish to examine further? Do you want to: 1) Gently reach for a glowing fruit 2) Peer into the hollow of the tree 3) Sit down and meditate. What does your character choose to do?"

                         [PlayerAgency: 4] (Good Agency Respect)
                         Definition: The response respects player agency with only minor issues. It generally allows the player to make their own decisions and describes the world and consequences without taking actions for the player. There may be occasional minor assumptions or soft suggestions embedded in prose, but no explicit numbered/bulleted choice lists. A response at this level might ask an open-ended question like "What do you do?" or present subtle hints in the prose rather than explicit menu options.

                         Examples:
                         Response: "The door stands before you, slightly ajar. A faint light spills from the crack. The air carries a musty scent, and you hear distant sounds echoing from within."

                         Response: "The merchant eyes you warily as you approach. His hand moves toward a hidden weapon, but he hasn't drawn it yet. The marketplace bustles around you, and other shoppers seem to be watching the interaction."

                         Response: "The path splits ahead. Would you like to follow the left trail into the forest, take the right path toward the mountains, or investigate the strange markings on the ground?"

                         Response: "You've reached the tavern. The bartender polishes a glass while eyeing you curiously, and a hooded figure in the corner seems to be watching. What do you do?"

                         [PlayerAgency: 5] (Exceptional Agency Respect)
                         Definition: The response perfectly respects player agency. It describes the world, situations, and consequences without making any assumptions about what the player will do. It never takes actions on the player's behalf, never presents numbered/bulleted choice lists, and never asks players to choose from explicit options. The player feels fully in control of their character's decisions and actions. The response may end with an open-ended prompt like "What do you do?" but never suggests specific actions.

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

        string responseText = await GetEvaluationResponseAsync(prompt, cancellationToken);

        // Parse the response
        EvaluationParseResult parseResult = ParseEvaluationResponse(responseText);

        // Create metric with standard diagnostics
        NumericMetric metric = CreateMetric(parseResult, responseText);

        return new EvaluationResult(metric);
    }
}

