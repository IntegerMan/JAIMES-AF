namespace MattEland.Jaimes.Domain;

public class MessageDto
{
    public int Id { get; init; }
    public required string Text { get; init; }
    public string? PlayerId { get; init; }
    public required string ParticipantName { get; init; }
    public DateTime CreatedAt { get; init; }
    public required string AgentId { get; init; }
    public string? AgentName { get; init; }
    public required int InstructionVersionId { get; init; }
    public string? VersionNumber { get; init; }
    public bool IsScriptedMessage { get; init; }

    // Sentiment analysis result: -1 (negative), 0 (neutral), 1 (positive), null (not analyzed)
    public int? Sentiment { get; init; }

    // Sentiment analysis confidence (0.0 to 1.0), null if not analyzed or legacy data
    public double? SentimentConfidence { get; init; }

    // Source of sentiment: 0 (Model), 1 (Player), null if not analyzed
    public int? SentimentSource { get; init; }

    // Sentiment record ID for navigation to details page
    public int? SentimentId { get; init; }

    public string? ModelName { get; init; }
    public string? ModelProvider { get; init; }
    public string? ModelEndpoint { get; init; }

    /// <summary>
    /// True if this message is missing any registered evaluators.
    /// </summary>
    public bool HasMissingEvaluators { get; set; }
}