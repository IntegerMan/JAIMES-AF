namespace MattEland.Jaimes.Workers.AssistantMessageWorker.Services;

/// <summary>
/// Service for evaluating assistant messages using AI evaluation metrics.
/// </summary>
public interface IMessageEvaluationService
{
    /// <summary>
    /// Evaluates an assistant message using the RelevanceTruthAndCompletenessEvaluator
    /// and stores the evaluation metrics in the database.
    /// </summary>
    /// <param name="message">The assistant message to evaluate.</param>
    /// <param name="systemPrompt">The system prompt/instructions used for the conversation.</param>
    /// <param name="conversationContext">The conversation context (last 5 messages).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the async operation.</returns>
    Task EvaluateMessageAsync(
        Message message,
        string systemPrompt,
        List<Message> conversationContext,
        CancellationToken cancellationToken = default);
}
