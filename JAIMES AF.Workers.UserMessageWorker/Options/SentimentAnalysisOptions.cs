namespace MattEland.Jaimes.Workers.UserMessageWorker.Options;

public class SentimentAnalysisOptions
{
    public const string SectionName = "SentimentAnalysis";

    /// <summary>
    /// Minimum confidence score (0.0 to 1.0) required to classify a message as positive or negative.
    /// If the confidence score is below this threshold, the message will be classified as neutral (0).
    /// Default: 0.65 (65%)
    /// </summary>
    public double ConfidenceThreshold { get; set; } = 0.65;

    /// <summary>
    /// Whether to reclassify the sentiment of all user messages in the Messages table on startup.
    /// Default: true
    /// </summary>
    public bool ReclassifyAllUserMessagesOnStartup { get; set; } = true;
}

