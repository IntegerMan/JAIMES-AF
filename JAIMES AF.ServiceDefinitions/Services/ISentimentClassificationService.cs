namespace MattEland.Jaimes.ServiceDefinitions.Services;

/// <summary>
/// Service for classifying sentiment of user messages.
/// </summary>
public interface ISentimentClassificationService
{
    /// <summary>
    /// Classifies the sentiment of a message text.
    /// </summary>
    /// <param name="messageText">The text to classify.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple of (sentiment, confidence) where sentiment is -1 (negative), 0 (neutral), or 1 (positive), and confidence is 0.0 to 1.0.</returns>
    Task<(int Sentiment, double Confidence)> ClassifyAsync(string messageText, CancellationToken cancellationToken = default);
}
