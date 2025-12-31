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
    /// The sentiment was manually set by the player from the game UI.
    /// </summary>
    Player = 1,

    /// <summary>
    /// The sentiment was set by an admin from the admin pages.
    /// </summary>
    Admin = 2
}
