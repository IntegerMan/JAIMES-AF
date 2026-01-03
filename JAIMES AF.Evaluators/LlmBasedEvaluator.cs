using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

namespace MattEland.Jaimes.Evaluators;

/// <summary>
/// Abstract base class for evaluators that use an IChatClient to generate evaluation scores.
/// Provides common functionality for response parsing, metric creation, and LLM request execution.
/// </summary>
/// <param name="chatClient">The chat client to use for evaluation.</param>
public abstract class LlmBasedEvaluator(IChatClient chatClient) : IEvaluator
{
    /// <summary>
    /// Gets the chat client used for evaluation.
    /// </summary>
    protected IChatClient ChatClient { get; } = chatClient;

    /// <summary>
    /// Gets the name of the metric this evaluator produces.
    /// </summary>
    public abstract string EvaluatorMetricName { get; }

    /// <inheritdoc />
    public IReadOnlyCollection<string> EvaluationMetricNames => [EvaluatorMetricName];

    /// <inheritdoc />
    public abstract ValueTask<EvaluationResult> EvaluateAsync(
        IEnumerable<ChatMessage> messages,
        ChatResponse modelResponse,
        ChatConfiguration? chatConfiguration = null,
        IEnumerable<EvaluationContext>? evaluationContext = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts the system prompt and conversation messages from a list of chat messages.
    /// </summary>
    /// <param name="messages">The messages to extract from.</param>
    /// <returns>A tuple containing the system prompt (if any) and the list of non-system messages.</returns>
    protected static (string? SystemPrompt, List<ChatMessage> ConversationMessages) ExtractMessages(
        IEnumerable<ChatMessage> messages)
    {
        List<ChatMessage> messagesList = messages.ToList();
        string? systemPrompt = messagesList.FirstOrDefault(m => m.Role == ChatRole.System)?.Text;
        List<ChatMessage> conversationMessages = messagesList
            .Where(m => m.Role != ChatRole.System)
            .ToList();

        return (systemPrompt, conversationMessages);
    }

    /// <summary>
    /// Executes an LLM evaluation request with no tools enabled.
    /// </summary>
    /// <param name="prompt">The evaluation prompt to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response text from the LLM.</returns>
    protected async Task<string> GetEvaluationResponseAsync(string prompt, CancellationToken cancellationToken)
    {
        ChatOptions chatOptions = new()
        {
            Tools = [] // Ensure no tools are used
        };

        var requestMessages = new List<ChatMessage>
        {
            new(ChatRole.User, prompt)
        };

        ChatResponse response = await ChatClient.GetResponseAsync(requestMessages, chatOptions, cancellationToken);
        return response.Text ?? string.Empty;
    }

    /// <summary>
    /// Parses an evaluation response to extract the S0 (ThoughtChain), S1 (Explanation), and S2 (Score) tags.
    /// </summary>
    /// <param name="responseText">The response text from the LLM.</param>
    /// <returns>An <see cref="EvaluationParseResult"/> containing the parsed data.</returns>
    protected static EvaluationParseResult ParseEvaluationResponse(string? responseText)
    {
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

        return new EvaluationParseResult(score, thoughtChain, explanation, parseSuccess);
    }

    /// <summary>
    /// Creates a <see cref="NumericMetric"/> from an evaluation parse result with standard diagnostics.
    /// </summary>
    /// <param name="parseResult">The parsed evaluation result.</param>
    /// <param name="rawResponseText">The raw response text for diagnostic purposes if parsing failed.</param>
    /// <returns>A <see cref="NumericMetric"/> with the score, explanation, and diagnostics.</returns>
    protected NumericMetric CreateMetric(EvaluationParseResult parseResult, string? rawResponseText = null)
    {
        // Determine pass/fail (fail on 3 or below, pass on 4 or 5)
        bool passed = parseResult.Score >= 4;
        string passFailStatus = passed ? "Pass" : "Fail";

        NumericMetric metric = new(EvaluatorMetricName)
        {
            Value = parseResult.Score,
            Reason = parseResult.Explanation,
            Interpretation = new EvaluationMetricInterpretation(passed ? EvaluationRating.Good : EvaluationRating.Poor)
        };

        // Add comprehensive diagnostics
        metric.Diagnostics ??= [];

        if (parseResult.ParseSuccess)
        {
            metric.Diagnostics.Add(new EvaluationDiagnostic(
                EvaluationDiagnosticSeverity.Informational,
                $"{EvaluatorMetricName} Score: {parseResult.Score} ({passFailStatus})"));

            if (!string.IsNullOrWhiteSpace(parseResult.ThoughtChain))
            {
                metric.Diagnostics.Add(new EvaluationDiagnostic(
                    EvaluationDiagnosticSeverity.Informational,
                    $"ThoughtChain: {parseResult.ThoughtChain}"));
            }
        }
        else
        {
            string displayText = string.IsNullOrWhiteSpace(rawResponseText) ? "{Empty}" : rawResponseText;
            int length = Math.Min(200, displayText.Length);

            metric.Diagnostics.Add(new EvaluationDiagnostic(
                EvaluationDiagnosticSeverity.Warning,
                $"Failed to parse evaluation response. Raw response: {displayText[..length]}"));
        }

        return metric;
    }

    /// <summary>
    /// Builds a simple conversation history string from a list of messages.
    /// </summary>
    /// <param name="conversationMessages">The conversation messages (excluding system prompt).</param>
    /// <returns>A formatted string with role and text for each message.</returns>
    protected static string BuildSimpleConversationHistory(List<ChatMessage> conversationMessages)
    {
        return string.Join("\n", conversationMessages.Select(m => $"{m.Role}: {m.Text}"));
    }
}
