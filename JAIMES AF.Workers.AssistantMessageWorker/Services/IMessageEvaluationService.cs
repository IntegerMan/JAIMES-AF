namespace MattEland.Jaimes.Workers.AssistantMessageWorker.Services;

/// <summary>
/// Service for evaluating assistant messages using AI evaluation metrics.
/// </summary>
public interface IMessageEvaluationService
{
    /// <summary>
    /// Evaluates an assistant message using all configured evaluators
    /// and stores the evaluation metrics in the database.
    /// </summary>
    /// <param name="message">The assistant message to evaluate.</param>
    /// <param name="systemPrompt">The system prompt/instructions used for the conversation.</param>
    /// <param name="conversationContext">The conversation context (last 5 messages).</param>
    /// <param name="evaluatorsToRun">Optional list of evaluator class names to run. If null, all evaluators are run.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the async operation.</returns>
    Task EvaluateMessageAsync(
        Message message,
        string systemPrompt,
        List<Message> conversationContext,
        IEnumerable<string>? evaluatorsToRun = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates an assistant message using a single specified evaluator.
    /// Used for parallel evaluation across multiple worker instances.
    /// </summary>
    /// <param name="message">The assistant message to evaluate.</param>
    /// <param name="systemPrompt">The system prompt/instructions used for the conversation.</param>
    /// <param name="conversationContext">The conversation context.</param>
    /// <param name="evaluatorName">The class name of the evaluator to run.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the async operation.</returns>
    Task EvaluateSingleEvaluatorAsync(
        Message message,
        string systemPrompt,
        List<Message> conversationContext,
        string evaluatorName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the list of available evaluator names.
    /// </summary>
    /// <returns>List of evaluator class names.</returns>
    IReadOnlyList<string> GetAvailableEvaluatorNames();
}
