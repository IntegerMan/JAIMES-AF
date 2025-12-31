namespace MattEland.Jaimes.Workers.AssistantMessageWorker.Services;

/// <summary>
/// Options for the brevity evaluator.
/// </summary>
public class BrevityEvaluatorOptions
{
    /// <summary>
    /// The target number of characters for the response.
    /// </summary>
    public int TargetCharacters { get; set; } = 500;

    /// <summary>
    /// The margin within which a response is considered perfect.
    /// </summary>
    public int Margin { get; set; } = 100;
}
