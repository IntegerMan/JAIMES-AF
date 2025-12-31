using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.Options;
using System.ComponentModel;

namespace MattEland.Jaimes.ServiceLayer.Evaluators;

/// <summary>
/// An evaluator that grades responses by their brevity relative to a target length.
/// </summary>
[Description("Grades responses based on their length relative to a target character count.")]
public class BrevityEvaluator(IOptions<BrevityEvaluatorOptions> options) : IEvaluator
{
    public const string BrevityMetricName = "Brevity";

    public IReadOnlyCollection<string> EvaluationMetricNames => [BrevityMetricName];

    public ValueTask<EvaluationResult> EvaluateAsync(
        IEnumerable<ChatMessage> messages,
        ChatResponse modelResponse,
        ChatConfiguration? chatConfiguration = null,
        IEnumerable<EvaluationContext>? evaluationContext = null,
        CancellationToken cancellationToken = default)
    {
        string text = modelResponse.Text;
        int charCount = text.Length;
        int wordCount = text.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;

        BrevityEvaluatorOptions config = options.Value;
        int target = config.TargetCharacters;
        int margin = config.Margin;

        if (margin <= 0)
        {
            throw new InvalidOperationException("Brevity margin must be greater than zero.");
        }

        int score;
        if (Math.Abs(charCount - target) <= margin)
        {
            score = 5;
        }
        else
        {
            // Deduct 1 per margin quantity over or under
            int deviation = Math.Abs(charCount - target) - margin;
            int deduction = (int)Math.Ceiling((double)deviation / margin);
            score = Math.Max(1, 5 - deduction);
        }

        string reasoning = score switch
        {
            5 => "The response length is ideal for a game master's reply.",
            4 => "The response length is acceptable, though it deviates slightly from the preferred length.",
            3 => "The response length is noticeably different from the target length.",
            2 => "The response length deviates significantly from the desired brevity.",
            _ => "The response length is poorly suited for the intended context."
        };

        NumericMetric metric = new(BrevityMetricName)
        {
            Value = score,
            Reason = reasoning
        };

        // Add additional information as diagnostics for this metric specifically
        metric.Diagnostics ??= [];
        metric.Diagnostics.Add(new EvaluationDiagnostic(
            EvaluationDiagnosticSeverity.Informational,
            $"Brevity Detail: {charCount} characters, {wordCount} words. Target: {target} (+/- {margin})"));

        EvaluationResult result = new([metric]);

        // Add additional information as metadata
        result.AddOrUpdateMetadataInAllMetrics("Brevity.CharacterCount", charCount.ToString());
        result.AddOrUpdateMetadataInAllMetrics("Brevity.WordCount", wordCount.ToString());
        result.AddOrUpdateMetadataInAllMetrics("Brevity.TargetCharacters", target.ToString());
        result.AddOrUpdateMetadataInAllMetrics("Brevity.Margin", margin.ToString());

        return new ValueTask<EvaluationResult>(result);
    }
}
