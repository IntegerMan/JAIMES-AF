namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// DTO for sentiment list view items.
/// </summary>
public class SentimentListItemDto
{
    /// <summary>
    /// Gets or sets the unique identifier for this sentiment record.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the ID of the message this sentiment belongs to.
    /// </summary>
    public int MessageId { get; set; }

    /// <summary>
    /// Gets or sets the sentiment value: -1 (negative), 0 (neutral), 1 (positive).
    /// </summary>
    public int Sentiment { get; set; }

    /// <summary>
    /// Gets or sets the source of the sentiment: 0 (Model), 1 (Player), 2 (Admin).
    /// </summary>
    public int SentimentSource { get; set; }

    /// <summary>
    /// Gets or sets the confidence score for the sentiment prediction (0.0 to 1.0).
    /// </summary>
    public double? Confidence { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this sentiment was first analyzed (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this sentiment was last updated (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the game ID.
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Gets or sets the player name for the game.
    /// </summary>
    public string? GamePlayerName { get; set; }

    /// <summary>
    /// Gets or sets the scenario name for the game.
    /// </summary>
    public string? GameScenarioName { get; set; }

    /// <summary>
    /// Gets or sets the ruleset ID for the game.
    /// </summary>
    public string? GameRulesetId { get; set; }

    /// <summary>
    /// Gets or sets the agent version number.
    /// </summary>
    public string? AgentVersion { get; set; }

    /// <summary>
    /// Gets or sets the agent identifier.
    /// </summary>
    public string? AgentId { get; set; }

    /// <summary>
    /// Gets or sets the list of tool names used in the AI response message.
    /// </summary>
    public List<string>? ToolNames { get; set; }

    /// <summary>
    /// Gets or sets whether the AI response has feedback.
    /// </summary>
    public bool HasFeedback { get; set; }

    /// <summary>
    /// Gets or sets whether the feedback (if any) is positive.
    /// </summary>
    public bool? FeedbackIsPositive { get; set; }

    /// <summary>
    /// Gets or sets a preview of the message text.
    /// </summary>
    public string? MessagePreview { get; set; }
}
