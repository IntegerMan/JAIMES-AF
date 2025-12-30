namespace MattEland.Jaimes.Repositories.Entities;

/// <summary>
/// Indicates the source of the sentiment analysis.
/// </summary>
public enum SentimentSource
{
    /// <summary>
    /// The sentiment was determined by an AI model.
    /// </summary>
    Model = 0,

    /// <summary>
    /// The sentiment was manually set by a player/admin.
    /// </summary>
    Player = 1
}
