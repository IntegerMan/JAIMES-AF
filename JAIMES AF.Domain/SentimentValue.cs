namespace MattEland.Jaimes.Domain;

/// <summary>
/// Defines the valid sentiment values used throughout the application.
/// Sentiment is always one of: Negative (-1), Neutral (0), or Positive (1).
/// </summary>
public static class SentimentValue
{
    /// <summary>
    /// Represents a negative sentiment (-1).
    /// </summary>
    public const int Negative = -1;

    /// <summary>
    /// Represents a neutral sentiment (0).
    /// </summary>
    public const int Neutral = 0;

    /// <summary>
    /// Represents a positive sentiment (1).
    /// </summary>
    public const int Positive = 1;

    /// <summary>
    /// Determines if the given sentiment value represents a positive sentiment.
    /// </summary>
    /// <param name="sentiment">The sentiment value to check.</param>
    /// <returns>True if the sentiment equals <see cref="Positive"/> (1).</returns>
    public static bool IsPositive(int sentiment) => sentiment == Positive;

    /// <summary>
    /// Determines if the given sentiment value represents a neutral sentiment.
    /// </summary>
    /// <param name="sentiment">The sentiment value to check.</param>
    /// <returns>True if the sentiment equals <see cref="Neutral"/> (0).</returns>
    public static bool IsNeutral(int sentiment) => sentiment == Neutral;

    /// <summary>
    /// Determines if the given sentiment value represents a negative sentiment.
    /// </summary>
    /// <param name="sentiment">The sentiment value to check.</param>
    /// <returns>True if the sentiment equals <see cref="Negative"/> (-1).</returns>
    public static bool IsNegative(int sentiment) => sentiment == Negative;

    /// <summary>
    /// Gets a human-readable label for the given sentiment value.
    /// </summary>
    /// <param name="sentiment">The sentiment value.</param>
    /// <returns>A string label: "Positive", "Neutral", "Negative", or "Unknown".</returns>
    public static string GetLabel(int sentiment) => sentiment switch
    {
        Positive => "Positive",
        Neutral => "Neutral",
        Negative => "Negative",
        _ => "Unknown"
    };
}
