using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

namespace MattEland.Jaimes.Workers.AssistantMessageWorker.Services;

/// <summary>
/// An evaluator that scores a conversation based on how much the AI assistant respected the player's agency.
/// </summary>
/// <param name="chatClient">The chat client to use for evaluation.</param>
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
        string prompt = $"""
                         You are an expert evaluator of AI responses in a role-playing game context. 
                         Your task is to score the AI's response based on how much it respected the player's agency.

                         Criteria:
                         - Score 5: The response perfectly respects the player's intent and does not make assumptions or take actions on behalf of the player.
                         - Score 4: The response respects agency but has minor issues.
                         - Score 3: The response has some issues with agency, or suggests a list of possible actions (which is a point deduction).
                         - Score 2: The response has significant issues with agency.
                         - Score 1: The response fails to respect player agency, makes major assumptions, or takes actions for the player.

                         A score of 3 or below is considered a failure. 
                         Suggesting a list of possible actions should result in a point deduction.

                         Please evaluate the following interaction:

                         Conversation History:
                         {string.Join("\n", messages.Select(m => $"{m.Role}: {m.Text}"))}

                         AI Response:
                         {modelResponse.Text}

                         Respond with a JSON object containing two fields:
                         - "score": An integer between 1 and 5.
                         - "reasoning": A brief explanation of the score.
                         """;

        ChatOptions chatOptions = new()
        {
            Tools = [], // Ensure no tools are used
            ResponseFormat = ChatResponseFormat.Json
        };

        var requestMessages = new List<ChatMessage>
        {
            new(ChatRole.User, prompt)
        };

        ChatResponse response = await chatClient.GetResponseAsync(requestMessages, chatOptions, cancellationToken);
        string responseText = response.Text ?? string.Empty;

        int score = 1;
        string reasoning = "Failed to parse evaluation response.";

        try
        {
            if (!string.IsNullOrWhiteSpace(responseText))
            {
                ChatBasedEvaluationResponse? evalResponse =
                    JsonSerializer.Deserialize<ChatBasedEvaluationResponse>(responseText, JsonSerializerOptions.Web);
                if (evalResponse != null)
                {
                    score = evalResponse.Score;
                    reasoning = evalResponse.Reasoning ?? string.Empty;
                }
            }
        }
        catch (JsonException)
        {
            // Fallback if JSON parsing fails, though with ResponseFormat.Json it should be reliable
            reasoning = $"Failed to parse JSON response: {responseText}";
        }

        // Ensure score is within range
        score = Math.Clamp(score, 1, 5);

        NumericMetric metric = new(MetricName)
        {
            Value = score,
            Reason = reasoning
        };

        // Add additional diagnostics
        metric.Diagnostics ??= [];
        metric.Diagnostics.Add(new EvaluationDiagnostic(
            EvaluationDiagnosticSeverity.Informational,
            $"Player Agency Score: {score}. Reasoning: {reasoning}"));

        return new EvaluationResult(metric);
    }

    private class ChatBasedEvaluationResponse
    {
        [JsonPropertyName("score")] public int Score { get; set; }

        [JsonPropertyName("reasoning")] public string? Reasoning { get; set; }
    }
}
